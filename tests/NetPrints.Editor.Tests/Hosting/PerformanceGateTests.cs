using System.Diagnostics;
using System.Globalization;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Hosting;

public sealed class PerformanceGateTests
{
    [Fact]
    public void TheMedianIsTheMiddleValue()
    {
        Assert.Equal(3, PerformanceGate.Median([5, 1, 3, 100, 2]));
    }

    [Fact]
    public async Task TheSteadyStateIsTheRequestedSamplesAfterTheWarmUp()
    {
        int calls = 0;
        SteadyState state = await PerformanceGate.MeasureAsync(async () =>
        {
            calls++;
            await Task.Delay(1, TestContext.Current.CancellationToken);
            return calls > 3 ? new Sample(2.0, 100, 0.5) : new Sample(9.0, 900, 0);
        }, 15);

        Assert.Equal(15, state.Count);
        Assert.Equal(2.0, state.Fastest);
        Assert.Equal(2.0, state.Median);
        Assert.Equal(100, state.Bytes);
        Assert.Equal(0.5, state.GcPause);
        Assert.Equal(calls - state.Count, state.WarmUpRuns);
        Assert.InRange(state.WarmUpSeconds, 1.0, 6.0);
    }

    [Fact]
    public void ASampleCountsItsBytesAndLeavesItsCollectionsOutOfItsTime()
    {
        var wall = Stopwatch.StartNew();
        SampleClock clock = PerformanceGate.StartSample();
        byte[][] blocks = [.. Enumerable.Range(0, 64).Select(_ => new byte[64 * 1024])];
        GC.Collect();
        Sample sample = clock.Stop();
        wall.Stop();

        Assert.Equal(64, blocks.Length);
        Assert.True(sample.Bytes >= 64 * 64 * 1024, $"The sample counted {sample.Bytes} bytes.");
        Assert.True(sample.GcPauseMilliseconds > 0, "The collection inside the sample paused nothing.");
        Assert.InRange(sample.Milliseconds, 0, wall.Elapsed.TotalMilliseconds - sample.GcPauseMilliseconds);
    }

    [Fact]
    public void ARecordIsOneInvariantLineTheScriptReads()
    {
        string path = Path.Combine(Path.GetTempPath(), "netprints-perf-tests", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"{path} has no folder."));
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            PerformanceGate.Record(path, "M2", new SteadyState(13.25, 14.5, 36_000_000, 1.25, 9, 40, 1.5, 2.25));
            PerformanceGate.Record(path, "M1", new SteadyState(61, 64.125, 17_000_000, 0, 21, 12, 1, 2));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        string[] lines = File.ReadAllLines(path);
        File.Delete(path);
        Assert.Equal(["M2 13.250 14.500 36000000 1.250 9 40 1.50 2.25", "M1 61.000 64.125 17000000 0.000 21 12 1.00 2.00"], lines);
    }
}
