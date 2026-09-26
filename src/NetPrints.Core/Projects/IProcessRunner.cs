#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Projects;

/// <summary>
/// Result of running a process to completion (project-system.md §4).
/// </summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="StandardOutput">Everything the process wrote to standard output.</param>
/// <param name="StandardError">Everything the process wrote to standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs an external process and captures its output, on behalf of <see cref="IProjectSystem"/>
/// implementations (project-system.md §4). Kept separate from <see cref="IProjectSystem"/> itself so
/// tests can substitute a fake without spawning real processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts <paramref name="request"/> and waits for it to exit, capturing both output streams.
    /// </summary>
    /// <param name="request">Process to start.</param>
    /// <param name="cancellationToken">Cancels the wait; the implementation kills the process tree
    /// when cancellation is requested.</param>
    /// <returns>The process's exit code and captured output.</returns>
    Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken);
}
