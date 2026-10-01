using NetPrints.Projects;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Follows the last launched program through <see cref="IProcessLauncher"/>'s events and the
/// compile flow (<see cref="BuildStarted"/>, <see cref="BuildFinished"/>), keeping the tail of its
/// output, so a slow run can be told apart from lost output (issue #11). Safe to use from any thread.
/// </summary>
public sealed class RunStateTracker : IDisposable
{
    /// <summary>How many lines of each stream are kept.</summary>
    public const int TailLines = 200;

    private readonly Lock gate = new();
    private readonly Queue<string> stdout = new();
    private readonly Queue<string> stderr = new();
    private readonly IProcessLauncher launcher;
    private RunPhase phase;
    private int? exitCode;

    /// <summary>Starts following <paramref name="launcher"/>.</summary>
    /// <param name="launcher">The launcher the editor starts programs through.</param>
    public RunStateTracker(IProcessLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        this.launcher = launcher;
        launcher.ProcessStarted += OnStarted;
        launcher.LineReceived += OnLine;
        launcher.ProcessExited += OnExited;
    }

    /// <summary>A compile began: forgets the previous run.</summary>
    public void BuildStarted()
    {
        lock (gate)
        {
            Reset(RunPhase.Building);
        }
    }

    /// <summary>A compile ended; back to <see cref="RunPhase.NotStarted"/> unless a program was started since.</summary>
    public void BuildFinished()
    {
        lock (gate)
        {
            if (phase == RunPhase.Building)
            {
                phase = RunPhase.NotStarted;
            }
        }
    }

    /// <summary>The current state.</summary>
    /// <returns>A snapshot that later events do not change.</returns>
    public RunStateSnapshot Snapshot()
    {
        lock (gate)
        {
            return new RunStateSnapshot(phase, exitCode, [.. stdout], [.. stderr]);
        }
    }

    /// <summary>Stops following the launcher.</summary>
    public void Dispose()
    {
        launcher.ProcessStarted -= OnStarted;
        launcher.LineReceived -= OnLine;
        launcher.ProcessExited -= OnExited;
    }

    private void Reset(RunPhase next)
    {
        phase = next;
        exitCode = null;
        stdout.Clear();
        stderr.Clear();
    }

    private void OnStarted(ProcessStartRequest request)
    {
        lock (gate)
        {
            Reset(RunPhase.Running);
        }
    }

    private void OnLine(ProcessStream stream, string line)
    {
        lock (gate)
        {
            if (phase != RunPhase.Running)
            {
                return;
            }

            var tail = stream == ProcessStream.Error ? stderr : stdout;
            tail.Enqueue(line);
            while (tail.Count > TailLines)
            {
                tail.Dequeue();
            }
        }
    }

    private void OnExited(int code)
    {
        lock (gate)
        {
            if (phase == RunPhase.Running)
            {
                phase = RunPhase.Exited;
                exitCode = code;
            }
        }
    }
}
