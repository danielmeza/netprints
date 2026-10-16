using System.Diagnostics;
using System.Globalization;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// M1 baseline (research R4 step 1, SC-005): the editor's startup on the headless platform, from nothing to a ready
/// shell. One run builds the composition and the shell host (<c>ShellApp.Start</c>), and the shell view model is ready
/// when the call returns. The first run in the process also pays the one-time JIT and Avalonia setup, so it is
/// reported apart; the median of the later runs is the baseline. Time and allocated bytes go to the test output.
/// The numbers pin today's hand-wired behaviour and carry no budget; the composition step C1 compares against them.
/// </summary>
public class ShellStartupPerformanceTests
{
    private const int Runs = 6;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    [Trait("Category", "Performance")]
    public async Task StartingTheShellIsMeasured()
    {
        var output = TestContext.Current.TestOutputHelper
            ?? throw new InvalidOperationException($"{nameof(TestContext)} has no {nameof(TestContext.Current.TestOutputHelper)}.");

        var times = new List<double>();
        var bytes = new List<double>();
        for (int run = 1; run <= Runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            await using ShellApp app = ShellApp.Start();
            stopwatch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.NotNull(app.Shell);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"M1 run {run}/{Runs}{(run == 1 ? " (cold)" : string.Empty)}: composition and ready shell {stopwatch.Elapsed.TotalMilliseconds:F1} ms, {allocated} bytes"));
            if (run > 1)
            {
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
                bytes.Add(allocated);
            }
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"M1 median of the warm runs: {Median(times):F1} ms, {Median(bytes):F0} bytes"));
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}
