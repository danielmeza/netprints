using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Cli.Infrastructure;
using NetPrints.Compilation;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints migrate [&lt;path&gt;...]</c>: reads each graph through its <see cref="IDocumentFormat"/> and reports its schema
/// version. While version 1 is current no migration exists, so nothing is written (contracts/cli.md §4).
/// </summary>
internal sealed class MigrateCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    Lazy<IProjectSystem> projects) : AsyncCommand<MigrateSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "migrate";

    private const string GraphExtension = ".netpc.json";
    private const string ProjectExtension = ".csproj";
    private static readonly string[] SkippedDirectories = ["bin", "obj"];

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string[] arguments = settings.Paths.Length == 0 ? [""] : settings.Paths;
        var graphs = new SortedSet<string>(StringComparer.Ordinal);
        var extensionFolders = new SortedSet<string>(StringComparer.Ordinal);
        var unreadable = new List<string>();
        foreach (string argument in arguments)
        {
            CollectResult result = await CollectAsync(argument, graphs, extensionFolders, unreadable, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != ExitCodes.Success)
            {
                if (result.Message is not null)
                {
                    await environment.Error.WriteLineAsync(result.Message).ConfigureAwait(false);
                }

                return result.ExitCode;
            }
        }

        (ExtensionRegistry registry, IReadOnlyList<CodeDiagnostic> extensionDiagnostics) = GraphCodeGenerator.LoadExtensions(
            new GenerateRequest(string.Empty, null, string.Empty, [], [.. extensionFolders]), cancellationToken);
        await using (registry.ConfigureAwait(false))
        {
            if (extensionDiagnostics.Count > 0)
            {
                foreach (CodeDiagnostic diagnostic in extensionDiagnostics)
                {
                    console.WriteLineRaw(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
                }

                return ExitCodes.Failed;
            }

            var formats = new DocumentFormatRegistry([
                new JsonDocumentFormat(new NetPrintsJsonOptions(registry.NodeConverters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance)),
            ]);
            return await ReportAllAsync(graphs, unreadable, formats, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> ReportAllAsync(SortedSet<string> graphs, List<string> unreadable, DocumentFormatRegistry formats, CancellationToken cancellationToken)
    {
        foreach (string line in unreadable)
        {
            console.WriteLineRaw(line);
        }

        int failures = 0;
        foreach (string graph in graphs)
        {
            if (!await ReportAsync(graph, formats, cancellationToken).ConfigureAwait(false))
            {
                failures++;
            }
        }

        console.WriteLineRaw(failures == 0 && unreadable.Count == 0
            ? $"No migrations are available; {graphs.Count} graph(s) are at schema version {DocumentMigrator.CurrentSchemaVersion}."
            : unreadable.Count == 0
                ? $"{failures} of {graphs.Count} graph(s) could not be read."
                : $"{failures} of {graphs.Count} graph(s) and {unreadable.Count} director(ies) could not be read.");
        return failures == 0 && unreadable.Count == 0 ? ExitCodes.Success : ExitCodes.Failed;
    }

    private async Task<CollectResult> CollectAsync(string argument, SortedSet<string> graphs, SortedSet<string> extensionFolders, List<string> unreadable, CancellationToken cancellationToken)
    {
        string path = Path.GetFullPath(argument.Length == 0 ? "." : argument, environment.CurrentDirectory);

        if (argument.Length > 0 && File.Exists(path) && path.EndsWith(GraphExtension, StringComparison.OrdinalIgnoreCase))
        {
            graphs.Add(path);
            return CollectResult.Ok;
        }

        if (argument.Length > 0 && Directory.Exists(path))
        {
            CollectGraphs(path, graphs, unreadable);
            return CollectResult.Ok;
        }

        if (argument.Length > 0 && !File.Exists(path))
        {
            return CollectResult.Fail(ExitCodes.Usage, $"'{path}' does not exist.");
        }

        if (argument.Length > 0 && !path.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase))
        {
            return CollectResult.Fail(ExitCodes.Usage, $"'{path}' is neither a {GraphExtension} graph, a {ProjectExtension} project nor a directory.");
        }

        ProjectLocation location = ProjectLocator.Locate(argument.Length == 0 ? null : argument, environment);
        if (location.Path is not { } projectPath)
        {
            return CollectResult.Fail(ExitCodes.Usage, location.Error ?? "The project could not be resolved.");
        }

        if (!msBuild.EnsureRegistered(loggerFactory.CreateLogger(nameof(IMsBuildRegistration))))
        {
            return CollectResult.Fail(ExitCodes.NoSdk, "No .NET SDK could be found; nothing was checked.");
        }

        ProjectSnapshot snapshot;
        try
        {
            snapshot = await projects.Value.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
        }
        catch (ProjectSystemException ex)
        {
            return CollectResult.Fail(ex.Code == ProjectSystemException.NoSdkRegistered ? ExitCodes.NoSdk : ExitCodes.Failed, $"{projectPath}: {ex.Message}");
        }

        if (ProjectMessageFormat.WriteErrors(snapshot.Messages, environment.Error))
        {
            return CollectResult.Fail(ExitCodes.Failed, null);
        }

        foreach (string file in snapshot.GraphFiles)
        {
            graphs.Add(Path.GetFullPath(file, Path.GetDirectoryName(projectPath) ?? environment.CurrentDirectory));
        }

        foreach (string folder in snapshot.ExtensionFolders)
        {
            extensionFolders.Add(Path.GetFullPath(folder, Path.GetDirectoryName(projectPath) ?? environment.CurrentDirectory));
        }

        return CollectResult.Ok;
    }

    // Symbolic links and junctions are not followed (a link back up the tree would rescan it), and a directory that cannot be listed is
    // reported and counted as a failure instead of aborting the walk.
    private void CollectGraphs(string directory, ICollection<string> graphs, ICollection<string> unreadable)
    {
        string[] files;
        string[] children;
        try
        {
            files = Directory.GetFiles(directory, "*" + GraphExtension, SearchOption.TopDirectoryOnly);
            children = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            unreadable.Add($"{Path.GetRelativePath(environment.CurrentDirectory, directory)}: unreadable: {ex.Message}");
            return;
        }

        foreach (string file in files.Where(file => file.EndsWith(GraphExtension, StringComparison.OrdinalIgnoreCase)))
        {
            graphs.Add(file);
        }

        foreach (string child in children)
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)
                || new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            CollectGraphs(child, graphs, unreadable);
        }
    }

    private readonly record struct CollectResult(int ExitCode, string? Message)
    {
        public static CollectResult Ok { get; } = new(ExitCodes.Success, null);

        public static CollectResult Fail(int exitCode, string? message) => new(exitCode, message);
    }

    private async Task<bool> ReportAsync(string graph, DocumentFormatRegistry formats, CancellationToken cancellationToken)
    {
        string display = Path.GetRelativePath(environment.CurrentDirectory, graph);
        var id = new DocumentId(Path.GetFileName(graph));
        IDocumentFormat? format = formats.Find(id, DocumentKind.Class);
        if (format is null)
        {
            console.WriteLineRaw($"{display}: unreadable: no document format handles this file.");
            return false;
        }

        try
        {
            using var store = new FileSystemDocumentStore(Path.GetDirectoryName(graph) ?? environment.CurrentDirectory, Scheduler.Default,
                NullLogger<FileSystemDocumentStore>.Instance, watch: false);
            await using Stream input = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false);
            await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
            console.WriteLineRaw($"{display}: schema {DocumentMigrator.CurrentSchemaVersion} (current)");
            return true;
        }
        catch (DocumentVersionException ex)
        {
            console.WriteLineRaw($"{display}: schema {ex.Found} is not supported (this tool supports {ex.Supported})");
            return false;
        }
        catch (Exception ex) when (ex is DocumentFormatException or IOException or DocumentNotFoundException or UnauthorizedAccessException)
        {
            console.WriteLineRaw($"{display}: unreadable: {ex.Message}");
            return false;
        }
    }
}
