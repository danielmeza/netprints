using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints git-install</c>: registers the graph diff text conversion and, with <c>--merge</c>, the merge driver in git's configuration and
/// attributes (contracts/git.md §3, FR-035, FR-036); <c>--uninstall</c> removes exactly what it added.
/// </summary>
internal sealed class GitInstallCommand(IAnsiConsole console, CliEnvironment environment, IProcessRunner processes) : AsyncCommand<GitInstallSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "git-install";

    private const string TextconvKey = "diff.netprints.textconv";
    private const string MergeNameKey = "merge.netprints.name";
    private const string MergeDriverKey = "merge.netprints.driver";
    private const string MergeName = "NetPrints graph merge";
    private const string HomePrefix = "~/";

    // Variables that decide which user configuration git reads; forwarded so the command and git agree on it.
    private static readonly string[] ForwardedVariables = ["HOME", "USERPROFILE", "XDG_CONFIG_HOME", "GIT_CONFIG_GLOBAL"];

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, GitInstallSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Command))
        {
            await environment.Error.WriteLineAsync("--command must not be empty.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        try
        {
            string? workTree = null;
            if (!settings.Global)
            {
                workTree = await FindWorkTreeAsync(cancellationToken).ConfigureAwait(false);
                if (workTree is null)
                {
                    await environment.Error.WriteLineAsync($"'{environment.CurrentDirectory}' is not inside a git work tree.").ConfigureAwait(false);
                    return ExitCodes.Usage;
                }
            }

            var config = new GitConfig(processes, workTree ?? environment.CurrentDirectory, settings.Global, ForwardedEnvironment());
            string attributes = workTree is null
                ? await GlobalAttributesPathAsync(config, cancellationToken).ConfigureAwait(false)
                : Path.Combine(workTree, ".gitattributes");
            return settings.Uninstall
                ? await UninstallAsync(config, attributes, cancellationToken).ConfigureAwait(false)
                : await InstallAsync(config, attributes, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception ex)
        {
            await environment.Error.WriteLineAsync($"git could not run: {ex.Message}").ConfigureAwait(false);
            return ExitCodes.Failed;
        }
    }

    private async Task<int> InstallAsync(GitConfig config, string attributesPath, GitInstallSettings settings, CancellationToken cancellationToken)
    {
        GitAttributesFile attributes = GitAttributesFile.Parse(await ReadAsync(attributesPath, cancellationToken).ConfigureAwait(false));
        if (attributes.ConflictingLine is { } conflicting)
        {
            await environment.Error.WriteLineAsync($"{attributesPath}: '{conflicting}' already names another driver for graphs; kept, nothing written.").ConfigureAwait(false);
            return ExitCodes.Failed;
        }

        var wanted = new List<KeyValuePair<string, string>> { new(TextconvKey, $"{settings.Command} show") };
        if (settings.Merge)
        {
            wanted.Add(new(MergeNameKey, MergeName));
            wanted.Add(new(MergeDriverKey, $"{settings.Command} merge %O %A %B --marker-size %L --path %P"));
        }

        foreach ((string key, string value) in wanted)
        {
            if (await config.GetAsync(key, cancellationToken).ConfigureAwait(false) == value)
            {
                await WriteAsync($"already installed: git config {key}").ConfigureAwait(false);
                continue;
            }

            GitConfigChange change = await config.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
            if (change.Error is not null)
            {
                await environment.Error.WriteLineAsync(change.Error).ConfigureAwait(false);
                return ExitCodes.Failed;
            }

            await WriteAsync($"installed: git config {key}").ConfigureAwait(false);
        }

        if (attributes.Covers(settings.Merge))
        {
            await WriteAsync($"already installed: {attributesPath}").ConfigureAwait(false);
            return ExitCodes.Success;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(attributesPath) ?? environment.CurrentDirectory);
        await File.WriteAllBytesAsync(attributesPath, attributes.WithLine(settings.Merge), cancellationToken).ConfigureAwait(false);
        await WriteAsync($"installed: {attributesPath}").ConfigureAwait(false);
        return ExitCodes.Success;
    }

    private async Task<int> UninstallAsync(GitConfig config, string attributesPath, CancellationToken cancellationToken)
    {
        bool removedAny = false;
        foreach (string key in new[] { TextconvKey, MergeNameKey, MergeDriverKey })
        {
            GitConfigChange change = await config.UnsetAsync(key, cancellationToken).ConfigureAwait(false);
            if (change.Error is not null)
            {
                await environment.Error.WriteLineAsync(change.Error).ConfigureAwait(false);
                return ExitCodes.Failed;
            }

            if (change.Changed)
            {
                removedAny = true;
                await WriteAsync($"removed: git config {key}").ConfigureAwait(false);
            }
        }

        GitAttributesFile attributes = GitAttributesFile.Parse(await ReadAsync(attributesPath, cancellationToken).ConfigureAwait(false));
        if (attributes.HasToolLine())
        {
            byte[] remaining = attributes.WithoutToolLines();
            if (GitAttributesFile.Parse(remaining).IsBlank)
            {
                File.Delete(attributesPath);
            }
            else
            {
                await File.WriteAllBytesAsync(attributesPath, remaining, cancellationToken).ConfigureAwait(false);
            }

            removedAny = true;
            await WriteAsync($"removed: {attributesPath}").ConfigureAwait(false);
        }

        if (!removedAny)
        {
            await WriteAsync("not installed: nothing to remove").ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }

    private async Task<string?> FindWorkTreeAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await processes.RunAsync(
            new ProcessStartRequest("git", ["rev-parse", "--show-toplevel"], environment.CurrentDirectory, ForwardedEnvironment()),
            cancellationToken).ConfigureAwait(false);
        string path = result.StandardOutput.Trim();
        return result.ExitCode == 0 && path.Length > 0 ? path : null;
    }

    private async Task<string> GlobalAttributesPathAsync(GitConfig config, CancellationToken cancellationToken)
    {
        string home = environment.GetVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? configured = await config.GetAsync("core.attributesFile", cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.StartsWith(HomePrefix, StringComparison.Ordinal)
                ? Path.Combine(home, configured[HomePrefix.Length..])
                : Path.GetFullPath(configured, environment.CurrentDirectory);
        }

        string? xdg = environment.GetVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "git", "attributes");
    }

    private Dictionary<string, string>? ForwardedEnvironment()
    {
        Dictionary<string, string>? forwarded = null;
        foreach (string name in ForwardedVariables)
        {
            if (environment.GetVariable(name) is { Length: > 0 } value)
            {
                (forwarded ??= [])[name] = value;
            }
        }

        return forwarded;
    }

    private static async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : [];

    private Task WriteAsync(string line) => console.Profile.Out.Writer.WriteLineAsync(line);
}
