using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Workspace.Run;

/// <summary>What a front end asks <see cref="RunPlanner"/> to plan.</summary>
/// <param name="ProjectFilePath">The full path of the project file.</param>
/// <param name="Configuration">The build configuration.</param>
public sealed record RunRequest(string ProjectFilePath, string Configuration)
{
    /// <summary>Gets the project's output type; <see langword="null"/> when the planner has to evaluate it (the command line).</summary>
    public BinaryType? OutputType { get; init; }

    /// <summary>Gets the profile to run; <see langword="null"/> for the default profile.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gets a value indicating whether the project runs with no profile.</summary>
    public bool NoLaunchProfile { get; init; }

    /// <summary>Gets the arguments that replace the profile's own (<c>netprints run -- args</c>); <see langword="null"/> keeps the profile's.</summary>
    public IReadOnlyList<string>? ArgumentOverride { get; init; }
}
