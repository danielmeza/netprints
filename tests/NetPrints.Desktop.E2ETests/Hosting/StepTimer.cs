using System.Diagnostics;
using System.Globalization;

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

    private static readonly Lock FileLock = new();
    private static readonly string SummaryPath = CreateSummaryFile();

    private readonly Lock gate = new();
    private readonly List<StepScope> steps = [];

    /// <summary>The folder the run's results go to: the parent of <c>NETPRINTS_UI_ARTIFACTS</c>, else <c>TestResults</c>.</summary>
    public static string ResultsDirectory { get; } = (Environment.GetEnvironmentVariable("NETPRINTS_UI_ARTIFACTS") is { Length: > 0 } artifactsDir
        ? Path.GetDirectoryName(artifactsDir)
        : null) ?? "TestResults";

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
        string? forced = forcedStep ?? Environment.GetEnvironmentVariable(ForceVariable);
        if (forced is { Length: > 0 } && OpenStep is { } open && open.Name == forced)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    /// <summary>The steps so far as a Markdown table, the open one marked <c>(running)</c>.</summary>
    public string Timings()
    {
        var text = new System.Text.StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"# {testName}").AppendLine()
            .AppendLine("| step | ms |").AppendLine("|---|---|");
        lock (gate)
        {
            foreach (var step in steps)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"| {step.Name}{(step.IsClosed ? "" : " (running)")} | {step.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} |");
            }
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
