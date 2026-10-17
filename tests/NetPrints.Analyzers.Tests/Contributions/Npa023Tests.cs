using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA023: two constant command ids are equal in one compilation.</summary>
public class Npa023Tests
{
    private const string Id = "NPA023";

    [Fact]
    public async Task TheSecondCommandWithAnIdIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.save", "x", handler);
                            _ = new CommandDescriptor({|NPA023:"a.save"|}, "y", handler);
                            _ = new CommandDescriptor({|NPA023:"a.save"|}, "z", handler);
                            _ = new CommandDescriptor("a.open", "x", handler);
                """),
            Id);
    }

    [Fact]
    public async Task AConcatenatedConstantCountsAsItsValue()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("netprints.command.save", "x", handler);
                            _ = new CommandDescriptor({|NPA023:Prefix + "save"|}, "y", handler);
                """),
            Id);
    }

    [Fact]
    public async Task DistinctIdsNonConstantIdsAndAPanelWithTheSameIdAreAccepted()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.save", "x", handler);
                            _ = new CommandDescriptor("a.open", "x", handler);
                            _ = new CommandDescriptor(text, "x", handler);
                            _ = new CommandDescriptor(text, "x", handler);
                            _ = new PanelDescriptor("a.save", "x");
                """),
            Id);
    }
}
