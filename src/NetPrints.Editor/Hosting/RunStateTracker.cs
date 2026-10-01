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
    private int currentId;

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

    /// <summary>Raised, from the thread that caused it and outside the tracker's lock, after <see cref="RunStateSnapshot.Phase"/> changed.</summary>
    public event EventHandler? PhaseChanged;

    /// <summary>A compile began: forgets the previous run.</summary>
    public void BuildStarted()
    {
        lock (gate)
        {
            Reset(RunPhase.Building);
        }

        PhaseChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A compile ended; back to <see cref="RunPhase.NotStarted"/> unless a program was started since.</summary>
    public void BuildFinished()
    {
        bool changed = false;
        lock (gate)
        {
            if (phase == RunPhase.Building)
            {
                phase = RunPhase.NotStarted;
                changed = true;
            }
        }

        if (changed)
        {
            PhaseChanged?.Invoke(this, EventArgs.Empty);
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

    private void OnStarted(int id, ProcessStartRequest request)
    {
        lock (gate)
        {
            Reset(RunPhase.Running);
            currentId = id;
        }

        PhaseChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLine(int id, ProcessStream stream, string line)
    {
        lock (gate)
        {
            if (phase != RunPhase.Running || id != currentId)
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

    private void OnExited(int id, int code)
    {
        bool changed = false;
        lock (gate)
        {
            if (phase == RunPhase.Running && id == currentId)
            {
                phase = RunPhase.Exited;
                exitCode = code;
                changed = true;
            }
        }

        if (changed)
        {
            PhaseChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
