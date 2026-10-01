using NetPrints.Projects;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Starts external processes (running compiled projects).
/// </summary>
public interface IProcessLauncher
{
    /// <summary>Starts an external process. Its stdout/stderr are reported through <see cref="OutputReceived"/>.</summary>
    /// <param name="request">The process to start, typically <see cref="IProjectSystem.GetRunCommand"/>'s
    /// result (project-system.md §4).</param>
    /// <param name="cancellationToken">Cancelling it kills the process and its child processes; a token that is already cancelled kills the process right after it starts.</param>
    void Start(ProcessStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// One line of a started process's stdout or stderr, or a status line ("Process exited (code
    /// N).") — for the editor's Output pane. Every started process reports here, on every
    /// platform, whether or not the host has its own visible console for the child.
    /// </summary>
    event Action<string>? OutputReceived;

    /// <summary>Raised once a process was started, before any of its lines. The first argument is the id of this start,
    /// carried by the process's <see cref="LineReceived"/> and <see cref="ProcessExited"/> events.</summary>
    event Action<int, ProcessStartRequest>? ProcessStarted;

    /// <summary>One line of a started process's stdout or stderr, with the stream it came from (never the status line).</summary>
    event Action<int, ProcessStream, string>? LineReceived;

    /// <summary>The process with this start id exited with this code, after all of its lines were reported.</summary>
    event Action<int, int>? ProcessExited;
}
