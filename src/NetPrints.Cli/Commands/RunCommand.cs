using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary><c>netprints run [&lt;project&gt;] [-- &lt;args&gt;...]</c>: builds the project, runs the program and returns its exit code (contracts/cli.md §4).</summary>
internal sealed class RunCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    IProcessRunner processes,
    Lazy<IProjectSystem> projects) : ProjectCommandBase<ProjectSettings>(console, environment, msBuild, loggerFactory)
{
    /// <summary>The command name.</summary>
    public const string Name = "run";

    private const string ArgumentSeparator = "--";

    /// <inheritdoc/>
    protected override async Task<int> ExecuteProjectAsync(CommandContext context, string projectPath, ProjectSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IProjectSystem system = projects.Value;
        int buildExitCode = await BuildCommand.BuildAsync(system, Console, projectPath, cancellationToken).ConfigureAwait(false);
        if (buildExitCode != ExitCodes.Success)
        {
            return buildExitCode;
        }

        ProcessStartRequest request = system.GetRunCommand(projectPath);
        IReadOnlyList<string> programArguments = context.Remaining.Raw;
        if (programArguments.Count > 0)
        {
            request = request with { Arguments = [.. request.Arguments, ArgumentSeparator, .. programArguments] };
        }

        ProcessResult output = await processes.RunAsync(request, cancellationToken).ConfigureAwait(false);
        await Console.Profile.Out.Writer.WriteAsync(output.StandardOutput).ConfigureAwait(false);
        await Environment.Error.WriteAsync(output.StandardError).ConfigureAwait(false);
        return output.ExitCode;
    }
}
