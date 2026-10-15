namespace NetPrints.Editor.Hosting;

/// <summary>The state of the last launched program at one moment.</summary>
/// <param name="Phase">Where the program is in its life.</param>
/// <param name="ExitCode">The exit code once <paramref name="Phase"/> is <see cref="RunPhase.Exited"/>, otherwise <see langword="null"/>.</param>
/// <param name="Stdout">The last lines of standard output, oldest first (at most <see cref="RunStateTracker.TailLines"/>).</param>
/// <param name="Stderr">The last lines of standard error, oldest first (at most <see cref="RunStateTracker.TailLines"/>).</param>
public sealed record RunStateSnapshot(RunPhase Phase, int? ExitCode, IReadOnlyList<string> Stdout, IReadOnlyList<string> Stderr);
