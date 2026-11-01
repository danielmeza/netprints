namespace NetPrints.Workspace.Run;

/// <summary>Whether a profile can run a given project, and why not when it cannot.</summary>
/// <param name="CanRun">Whether the profile can run.</param>
/// <param name="Reason">The problem that disables the profile; <see langword="null"/> when it can run.</param>
public sealed record RunProfileAvailability(bool CanRun, RunProblem? Reason)
{
    /// <summary>The availability of a profile that can run.</summary>
    public static RunProfileAvailability Runnable { get; } = new(true, null);
}
