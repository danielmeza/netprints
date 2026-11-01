using System.Collections.Generic;
using System.Linq;

namespace NetPrints.Workspace.Run;

/// <summary>The result of choosing the profile to run.</summary>
/// <param name="Profile">The chosen profile; <see langword="null"/> when the project runs with no profile.</param>
/// <param name="Problems">The problems that block the choice; empty when a choice was made.</param>
public sealed record RunProfileChoice(LaunchProfile? Profile, IReadOnlyList<RunProblem> Problems)
{
    /// <summary>Gets a value indicating whether a problem of error severity stops the run.</summary>
    public bool IsBlocked => Problems.Any(problem => problem.Severity == RunProblemSeverity.Error);
}
