using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Hosting;

public sealed class PerformanceGateTests
{
    [Fact]
    public void AMeasurementTenPercentOverTheBaselineRatioPasses()
    {
        GateVerdict verdict = PerformanceGate.Judge("M2", 15.4, 10, 1.4);

        Assert.True(verdict.Passed, verdict.Message);
    }

    [Fact]
    public void AMeasurementOverTheGateFailsAndSaysByHowMuch()
    {
        GateVerdict verdict = PerformanceGate.Judge("M2", 20, 10, 1.0);

        Assert.False(verdict.Passed);
        Assert.Contains("+100.0%", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMedianIsTheMiddleValue()
    {
        Assert.Equal(3, PerformanceGate.Median([5, 1, 3, 100, 2]));
    }
}
