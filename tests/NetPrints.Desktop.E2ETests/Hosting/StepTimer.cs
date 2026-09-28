using System.Diagnostics;
using System.Globalization;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// Per-step timing for an E2E test (batch D2): <c>using (steps.Step("open project")) { ... }</c>
/// writes the step's duration to the test's own output and appends a row to a per-run summary
/// file (<c>TestResults/e2e-timings-&lt;run&gt;.md</c>, one file per <c>dotnet test</c> invocation),
/// so hot spots and before/after regressions can be read off after a run without re-instrumenting.
/// </summary>
public sealed class StepTimer(string testName)
{
    private static readonly Lock FileLock = new();
    private static readonly string SummaryPath = CreateSummaryFile();

    /// <summary>Starts timing a step; disposing the result records its duration.</summary>
    public IDisposable Step(string name) => new StepScope(this, name);

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
        string directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "TestResults")).FullName;
        string path = Path.Combine(directory, $"e2e-timings-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, $"# E2E step timings{Environment.NewLine}{Environment.NewLine}| test | step | ms |{Environment.NewLine}|---|---|---|{Environment.NewLine}");
        return path;
    }

    private sealed class StepScope(StepTimer timer, string name) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            timer.Record(name, clock.Elapsed);
        }
    }
}
