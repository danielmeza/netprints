using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary><c>netprints build [&lt;project&gt;]</c>: builds the project and reports its errors (contracts/cli.md §4).</summary>
internal sealed class BuildCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    Lazy<IProjectSystem> projects) : ProjectCommandBase<ProjectSettings>(console, environment, msBuild, loggerFactory)
{
    /// <summary>The command name.</summary>
    public const string Name = "build";

    /// <summary>Builds <paramref name="projectPath"/> and writes the errors and the summary line.</summary>
    /// <param name="projects">The project system that builds.</param>
    /// <param name="console">Receives the output.</param>
    /// <param name="projectPath">The full path of the project file.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns><see cref="ExitCodes.Success"/> when the build succeeded, otherwise <see cref="ExitCodes.Failed"/>.</returns>
    public static async Task<int> BuildAsync(IProjectSystem projects, IAnsiConsole console, string projectPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(console);

        BuildResult result = await projects.BuildAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            console.WriteLineRaw("Build succeeded.");
            return ExitCodes.Success;
        }

        ProjectMessage[] errors = result.Messages.Where(message => message.Severity == ProjectMessageSeverity.Error).ToArray();
        foreach (ProjectMessage error in errors)
        {
            console.WriteLineRaw(ProjectMessageFormat.ToLine(error));
        }

        console.WriteLineRaw($"Build failed with {errors.Length} error(s).");
        return ExitCodes.Failed;
    }

    /// <inheritdoc/>
    protected override Task<int> ExecuteProjectAsync(CommandContext context, string projectPath, ProjectSettings settings, CancellationToken cancellationToken) =>
        BuildAsync(projects.Value, Console, projectPath, cancellationToken);
}
