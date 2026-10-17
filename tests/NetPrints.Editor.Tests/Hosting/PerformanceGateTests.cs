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
    public void TheVerdictCarriesTheNumbersAndTheCpuOnPassAndOnFail()
    {
        foreach (GateVerdict verdict in new[] { PerformanceGate.Judge("M2", 10, 10, 1.0), PerformanceGate.Judge("M2", 30, 10, 1.0) })
        {
            Assert.Contains("a ratio of", verdict.Message, StringComparison.Ordinal);
            Assert.Contains("the baseline is 1.000", verdict.Message, StringComparison.Ordinal);
            Assert.Contains($"cpu {PerformanceGate.CpuModel}", verdict.Message, StringComparison.Ordinal);
        }

        Assert.False(string.IsNullOrWhiteSpace(PerformanceGate.CpuModel));
    }

    [Fact]
    public void TheBaselineComesFromTheMachineKindTheRunIsOn()
    {
        Assert.Equal(1.0, PerformanceGate.Baseline(1.0, 1.1, onCi: false));
        Assert.Equal(1.1, PerformanceGate.Baseline(1.0, 1.1, onCi: true));
    }

    [Fact]
    public void TheMedianIsTheMiddleValue()
    {
        Assert.Equal(3, PerformanceGate.Median([5, 1, 3, 100, 2]));
    }
}
