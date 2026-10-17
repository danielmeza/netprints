using System.Diagnostics;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;

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

    private const double BaselineRatio = 4.75;

    private const int CompositionBudgetMilliseconds = 150;

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

        double calibration = await PerformanceGate.CalibrateAsync();
        double steady = await PerformanceGate.SteadyMedianAsync(TimeOneStartAsync, BaselineRatio * PerformanceGate.Tolerance * calibration);
        calibration = Math.Min(calibration, await PerformanceGate.CalibrateAsync());
        GateVerdict verdict = PerformanceGate.Judge("M1 steady-state median", steady, calibration, BaselineRatio);
        output.WriteLine(verdict.Message);
        if (PerformanceGate.Strict)
        {
            Assert.True(verdict.Passed, verdict.Message);
        }
    }

    private static async ValueTask<double> TimeOneStartAsync()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var stopwatch = Stopwatch.StartNew();
        await using ShellApp app = ShellApp.Start();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    [Trait("Category", "Performance")]
    public async Task ComposingTheEditorStaysWithinTheBudget()
    {
        var output = TestContext.Current.TestOutputHelper
            ?? throw new InvalidOperationException($"{nameof(TestContext)} has no {nameof(TestContext.Current.TestOutputHelper)}.");
        await using var extensions = new ExtensionHost(new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry]), NullLoggerFactory.Instance);
        var settings = new JsonFileSettingsStore(Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"), "settings.json"), NullLogger<JsonFileSettingsStore>.Instance);
        var host = new EditorHostServices(NullLoggerFactory.Instance, extensions, settings, NullHostChannel.Instance, HostChannelError: null,
            MsBuildAvailable: false, DisposeOwnedResources: () => ValueTask.CompletedTask);

        var times = new List<double>();
        for (int run = 1; run <= Runs; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var composition = new TestComposition(host, new RecordingDialogs(), new QueuedFilePicker(), new CapturingProcessLauncher());
            stopwatch.Stop();
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Composition run {run}/{Runs}{(run == 1 ? " (cold)" : string.Empty)}: provider build and first resolves {stopwatch.Elapsed.TotalMilliseconds:F1} ms"));
            if (run > 1)
            {
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        double median = Median(times);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Composition median of the warm runs: {median:F1} ms"));
        Assert.True(median <= CompositionBudgetMilliseconds, $"Composition took {median:F1} ms, the budget is {CompositionBudgetMilliseconds} ms (ADR-0031).");
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}
