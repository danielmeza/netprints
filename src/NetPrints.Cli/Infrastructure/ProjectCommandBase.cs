using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>
/// A command that acts on one project: resolves the project argument, then makes sure a .NET SDK is registered
/// before <see cref="ExecuteProjectAsync"/> touches any Microsoft.Build type (contracts/cli.md §5).
/// </summary>
/// <typeparam name="TSettings">The command's settings.</typeparam>
internal abstract class ProjectCommandBase<TSettings>(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory) : AsyncCommand<TSettings>
    where TSettings : ProjectSettings
{
    /// <summary>Gets the process state (current directory, variables, stderr).</summary>
    protected CliEnvironment Environment { get; } = environment;

    /// <summary>Gets the console results and diagnostics are written to.</summary>
    protected IAnsiConsole Console { get; } = console;

    /// <inheritdoc/>
    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        ProjectLocation location = ProjectLocator.Locate(settings.Project, Environment);
        if (location.Path is not { } projectPath)
        {
            Console.WriteLineRaw(location.Error ?? "The project could not be resolved.");
            return ExitCodes.Usage;
        }

        if (!msBuild.EnsureRegistered(loggerFactory.CreateLogger(nameof(IMsBuildRegistration))))
        {
            Console.WriteLineRaw("No .NET SDK could be found; nothing was built.");
            return ExitCodes.NoSdk;
        }

        return await ExecuteProjectAsync(context, projectPath, settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the command against the resolved project once an SDK is registered.</summary>
    /// <param name="context">The command context, holding the arguments after <c>--</c>.</param>
    /// <param name="projectPath">The full path of the project file.</param>
    /// <param name="settings">The parsed settings.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The process exit code.</returns>
    protected abstract Task<int> ExecuteProjectAsync(CommandContext context, string projectPath, TSettings settings, CancellationToken cancellationToken);
}
