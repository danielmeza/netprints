using System.Collections.Generic;
using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA022: a constant contribution id follows the id pattern and the owner prefix.</summary>
public class Npa022Tests
{
    private const string Id = "NPA022";

    private static readonly Dictionary<string, string> Owner = new() { ["build_property.NetPrintsExtensionId"] = "acme.tools" };

    [Theory]
    [InlineData("Save")]
    [InlineData("Acme.save")]
    [InlineData("acme..save")]
    [InlineData("acme.")]
    [InlineData("acme.save now")]
    [InlineData("")]
    public async Task AnIdOffThePatternIsReported(string id)
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use($"            _ = new CommandDescriptor({{|NPA022:\"{id}\"|}}, \"x\", handler);"), Id);
    }

    [Fact]
    public async Task APanelIdIsCheckedToo()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use("            _ = new PanelDescriptor({|NPA022:\"Panel\"|}, \"x\");"), Id);
    }

    [Fact]
    public async Task AValidAConcatenatedAndANonConstantIdAreAccepted()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("acme.tools.save", "x", handler);
                            _ = new CommandDescriptor(Prefix + "open", "x", handler);
                            _ = new CommandDescriptor(text, "x", handler);
                """),
            Id);
    }

    [Fact]
    public async Task AnIdOutsideTheOwnerPrefixIsReportedWhereTheOwnerIsKnown()
    {
        await PinAnalyzerHarness.AssertDiagnosticsWithOptionsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("acme.tools.save", "x", handler);
                            _ = new CommandDescriptor({|NPA022:"other.save"|}, "x", handler);
                            _ = new CommandDescriptor({|NPA022:"acme.toolsish.save"|}, "x", handler);
                            _ = new PanelDescriptor({|NPA022:"acme.other"|}, "x");
                """),
            Owner,
            Id);
    }
}
