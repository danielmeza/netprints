using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using NetPrints.Workspace.Run;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints run [&lt;project&gt;] [-c &lt;name&gt;] [--launch-profile &lt;name&gt;] [--no-launch-profile] [-- &lt;args&gt;...]</c>: plans the run from the
/// launch profile, builds the project, runs the program and returns its exit code (contracts/cli.md §4, contracts/run-profiles.md §4).
/// </summary>
internal sealed class RunCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    IProgramRunner programs,
    ForwardedArguments forwarded,
    Lazy<IRunProjectSystem> projects,
    Lazy<RunPlanner> planner) : ProjectCommandBase<RunCommandSettings>(console, environment, msBuild, loggerFactory)
{
    /// <summary>The command name.</summary>
    public const string Name = "run";

    /// <inheritdoc/>
    protected override async Task<int> ExecuteProjectAsync(CommandContext context, string projectPath, RunCommandSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var request = new RunRequest(projectPath, settings.Configuration)
        {
            ProfileName = settings.LaunchProfile,
            NoLaunchProfile = settings.NoLaunchProfile,
            ArgumentOverride = forwarded.Values.Count > 0 ? forwarded.Values : null,
        };
        RunResolution resolution = await planner.Value.PlanAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (RunProblem problem in resolution.Problems)
        {
            await Environment.Error.WriteLineAsync(problem.ToLine()).ConfigureAwait(false);
        }

        if (resolution.Request is not { } process)
        {
            return RunExitCodes.For(resolution.Problems);
        }

        int buildExitCode = await BuildCommand.BuildAsync(projects.Value, Console, projectPath, settings.Configuration, cancellationToken).ConfigureAwait(false);
        if (buildExitCode != ExitCodes.Success)
        {
            return buildExitCode;
        }

        try
        {
            return await programs.RunAsync(process, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            var problem = new RunProblem(NprDiagnosticCodes.ExecutableNotFound, RunProblemSeverity.Error, $"The program '{process.FileName}' could not be started: {exception.Message}");
            await Environment.Error.WriteLineAsync(problem.ToLine()).ConfigureAwait(false);
            return ExitCodes.Failed;
        }
    }
}

/// <summary>Maps the problems that stopped a run to the exit code of <c>netprints run</c> (ADR-0015).</summary>
internal static class RunExitCodes
{
    /// <summary>Gets the exit code for a list of problems.</summary>
    /// <param name="problems">The problems found while planning, in order.</param>
    /// <returns>The code of the first error: <see cref="ExitCodes.Usage"/> for NPR003, NPR004 and NPR011, else <see cref="ExitCodes.Failed"/>; <see cref="ExitCodes.Success"/> when there is no error.</returns>
    public static int For(IReadOnlyList<RunProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        RunProblem? first = problems.FirstOrDefault(problem => problem.Severity == RunProblemSeverity.Error);
        if (first is null)
        {
            return ExitCodes.Success;
        }

        return first.Code is NprDiagnosticCodes.KindNotRunnable or NprDiagnosticCodes.UnknownProfile or NprDiagnosticCodes.ProjectKindNeedsExecutable
            ? ExitCodes.Usage
            : ExitCodes.Failed;
    }
}
