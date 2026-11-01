using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Workspace.Run;

/// <summary>One entry of <c>Properties/launchSettings.json</c>: the subset of its members NetPrints reads.</summary>
public sealed record LaunchProfile
{
    /// <summary>The profile name, the key in the file. Compared case-insensitively.</summary>
    public required string Name { get; init; }

    /// <summary>The <c>commandName</c> as written; case-sensitive.</summary>
    public required string CommandName { get; init; }

    /// <summary>The <c>executablePath</c> as written, or <see langword="null"/> when the file has none.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>The <c>commandLineArgs</c> as written, or <see langword="null"/> when the file has none.</summary>
    public string? CommandLineArgs { get; init; }

    /// <summary>The <c>workingDirectory</c> as written, or <see langword="null"/> when the file has none.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>The <c>environmentVariables</c> in file order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> EnvironmentVariables { get; init; } = [];

    /// <summary>Whether this profile can run a project with the given output type.</summary>
    /// <param name="outputType">The project's output type.</param>
    /// <returns>NPR003 for a kind NetPrints cannot run, then NPR011 for a <c>Project</c> profile on a library; otherwise runnable.</returns>
    public RunProfileAvailability AvailabilityFor(BinaryType outputType)
    {
        if (!RunProfileKinds.IsRunnable(CommandName))
        {
            return Disabled(NprDiagnosticCodes.KindNotRunnable, $"NetPrints cannot run {CommandName} profiles.");
        }

        if (outputType != BinaryType.Executable && string.Equals(CommandName, RunProfileKinds.Project, StringComparison.Ordinal))
        {
            return Disabled(
                NprDiagnosticCodes.ProjectKindNeedsExecutable,
                $"The Project kind runs only an executable project; this project's output type is {outputType}.");
        }

        return RunProfileAvailability.Runnable;
    }

    private static RunProfileAvailability Disabled(string code, string message) =>
        new(false, new RunProblem(code, RunProblemSeverity.Error, message));
}
