using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// Per-step timing for an E2E test (batch D2): <c>using (steps.Step("open project")) { ... }</c>
/// writes the step's duration to the test's own output and appends a row to a per-run summary
/// file (<c>TestResults/e2e-timings-&lt;run&gt;.md</c>, one file per <c>dotnet test</c> invocation),
/// so hot spots and before/after regressions can be read off after a run without re-instrumenting.
/// </summary>
public sealed class StepTimer(string testName, string? forcedStep = null)
{
    /// <summary>Names a step to hold until the test is cancelled, for a manual run of one class (see <see cref="HoldIfForcedAsync"/>).</summary>
    public const string ForceVariable = "NETPRINTS_E2E_FORCE_TIMEOUT";

    /// <summary>The folder the run's results go to: the parent of <c>NETPRINTS_UI_ARTIFACTS</c>, else <c>TestResults</c>.</summary>
    public static string ResultsDirectory { get; } = (Environment.GetEnvironmentVariable("NETPRINTS_UI_ARTIFACTS") is { Length: > 0 } artifactsDir
        ? Path.GetDirectoryName(artifactsDir)
        : null) ?? "TestResults";

    private static readonly Lock FileLock = new();
    private static readonly string SummaryPath = CreateSummaryFile();

    private readonly Lock gate = new();
    private bool held;
    private readonly List<StepScope> steps = [];
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Exception, FailureMoment> thrown = [];

    /// <summary>The step that is open now and how long it has been running, or <see langword="null"/> between steps.</summary>
    public (string Name, TimeSpan Elapsed)? OpenStep
    {
        get
        {
            lock (gate)
            {
                return steps.LastOrDefault(s => !s.IsClosed) is { } open ? (open.Name, open.Elapsed) : null;
            }
        }
    }

    /// <summary>Starts timing a step; disposing the result records its duration.</summary>
    public IDisposable Step(string name)
    {
        var scope = new StepScope(this, name);
        lock (gate)
        {
            steps.Add(scope);
        }

        return scope;
    }

    /// <summary>
    /// Waits until <paramref name="cancellationToken"/> is cancelled when the open step is the one forced
    /// to time out (the constructor's <c>forcedStep</c>, else <see cref="ForceVariable"/>); returns at once otherwise.
    /// </summary>
    public async Task HoldIfForcedAsync(CancellationToken cancellationToken)
    {
        string? forced = Forced;
        if (forced is { Length: > 0 } && OpenStep is { } open && open.Name == forced)
        {
            held = true;
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    /// <summary>Fails when a step was forced to time out but no checkpoint inside it ever held it.</summary>
    public void EnsureForcedStepHeld()
    {
        if (Forced is { Length: > 0 } forced && !held)
        {
            throw new InvalidOperationException($"step '{forced}' was never held (no checkpoint inside it); only a step that reaches a checkpoint can be forced to time out.");
        }
    }

    private string? Forced => forcedStep ?? Environment.GetEnvironmentVariable(ForceVariable);

    /// <summary>
    /// Remembers, for every exception thrown while the returned scope lives, which step was open at the
    /// first throw: by the time a failure reaches the scenario's caller the steps have been closed by
    /// their <c>using</c> blocks. Dispose to stop.
    /// </summary>
    public IDisposable WatchFailures()
    {
        void OnThrown(object? sender, FirstChanceExceptionEventArgs e) => thrown.TryAdd(e.Exception, Where());
        AppDomain.CurrentDomain.FirstChanceException += OnThrown;
        return new Unsubscribe(() => AppDomain.CurrentDomain.FirstChanceException -= OnThrown);
    }

    /// <summary>Where the test was when <paramref name="failure"/> (or an exception it wraps) was first thrown, else where it is now.</summary>
    public FailureMoment WhereFailed(Exception failure)
    {
        for (var e = failure; e is not null; e = e.InnerException)
        {
            if (thrown.TryGetValue(e, out var moment) && moment.Step is not null)
            {
                return moment;
            }
        }

        return Where();
    }

    private FailureMoment Where()
    {
        lock (gate)
        {
            var entries = steps.Select(s => new StepEntry(s.Name, s.Elapsed, !s.IsClosed)).ToList();
            var open = entries.LastOrDefault(e => e.Running);
            return new FailureMoment(open?.Name, open?.Elapsed ?? TimeSpan.Zero, entries);
        }
    }

    /// <summary>The steps of a moment (now by default) as a Markdown table, a running one marked <c>(running)</c>.</summary>
    public string Timings(FailureMoment? moment = null)
    {
        var text = new System.Text.StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"# {testName}").AppendLine()
            .AppendLine("| step | ms |").AppendLine("|---|---|");
        foreach (var step in (moment ?? Where()).Entries)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {step.Name}{(step.Running ? " (running)" : "")} | {step.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} |");
        }

        return text.ToString();
    }

    private void Record(string name, TimeSpan elapsed)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"[step] {testName} / {name}: {elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} ms");

        lock (FileLock)
        {
            File.AppendAllText(SummaryPath,
                $"| {testName} | {name} | {elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} |{Environment.NewLine}");
        }
    }

    private static string CreateSummaryFile()
    {
        string directory = Directory.CreateDirectory(ResultsDirectory).FullName;
        string path = Path.Combine(directory, $"e2e-timings-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, $"# E2E step timings{Environment.NewLine}{Environment.NewLine}| test | step | ms |{Environment.NewLine}|---|---|---|{Environment.NewLine}");
        return path;
    }

    private sealed class StepScope(StepTimer timer, string name) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();

        public string Name => name;

        public bool IsClosed { get; private set; }

        public TimeSpan Elapsed => clock.Elapsed;

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }

            clock.Stop();
            IsClosed = true;
            timer.Record(name, clock.Elapsed);
        }
    }
}

/// <summary>One step of a test: how long it ran, and whether it was still open.</summary>
/// <param name="Name">The step's name.</param>
/// <param name="Elapsed">How long it ran, or had run.</param>
/// <param name="Running">Whether it was open.</param>
public sealed record StepEntry(string Name, TimeSpan Elapsed, bool Running);

/// <summary>The state of a test's steps at one moment, usually the first throw of a failure.</summary>
/// <param name="Step">The open step's name, or <see langword="null"/> when none was open.</param>
/// <param name="Elapsed">How long that step had run.</param>
/// <param name="Entries">Every step so far, in order.</param>
public sealed record FailureMoment(string? Step, TimeSpan Elapsed, IReadOnlyList<StepEntry> Entries);

file sealed class Unsubscribe(Action action) : IDisposable
{
    public void Dispose() => action();
}
