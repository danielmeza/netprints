using System.Collections.Generic;
using NetPrints.Projects;

namespace NetPrints.Workspace.Run;

/// <summary>The outcome of resolving a run: the process to start, or the problems that stop it.</summary>
public sealed record RunResolution
{
    /// <summary>Gets the chosen profile; <see langword="null"/> when the project runs with no profile or the choice was blocked.</summary>
    public LaunchProfile? Profile { get; init; }

    /// <summary>Gets the process to start; <see langword="null"/> when an error problem blocks the run.</summary>
    public ProcessStartRequest? Request { get; init; }

    /// <summary>Gets the problems, in the order found.</summary>
    public IReadOnlyList<RunProblem> Problems { get; init; } = [];

    /// <summary>Gets the names of the variables added for the program, for the first-run print.</summary>
    public IReadOnlyList<string> AddedEnvironment { get; init; } = [];

    /// <summary>Gets a value indicating whether the run is stopped before the build.</summary>
    public bool IsBlocked => Request is null;
}
