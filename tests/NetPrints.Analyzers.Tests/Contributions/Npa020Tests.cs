using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA020: an icon id literal follows the icon id pattern and, for a built-in, is registered.</summary>
public class Npa020Tests
{
    private const string Id = "NPA020";

    [Theory]
    [InlineData("Save")]
    [InlineData("NetPrints.icon.save")]
    [InlineData("netprints..save")]
    [InlineData("netprints.icon.")]
    [InlineData("netprints.icon.save now")]
    public async Task AnIdOffThePatternIsReported(string icon)
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use($"            _ = new CommandDescriptor(\"a.b\", \"x\", handler, IconId: {{|NPA020:\"{icon}\"|}});"), Id);
    }

    [Fact]
    public async Task APanelIconIsCheckedToo()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use("            _ = new PanelDescriptor(\"a.b\", \"x\", {|NPA020:\"Panel\"|});"), Id);
    }

    [Fact]
    public async Task AnUnregisteredBuiltInIdIsReportedWhereTheRegistryIsKnown()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use("            _ = new CommandDescriptor(\"a.b\", \"x\", handler, IconId: {|NPA020:\"netprints.icon.nope\"|});"), Id);
    }

    [Fact]
    public async Task ABuiltInIdIsNotCheckedWhereTheRegistryIsNotInTheCompilation()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use("            _ = new CommandDescriptor(\"a.b\", \"x\", handler, IconId: \"netprints.icon.nope\");", withIcons: false), Id);
    }

    [Fact]
    public async Task ARegisteredIdAnExtensionIdAndANonConstantAreAccepted()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.b", "x", handler, IconId: NetPrints.Editor.Icons.IconIds.Save);
                            _ = new CommandDescriptor("a.c", "x", handler, IconId: "netprints.icon.save");
                            _ = new CommandDescriptor("a.d", "x", handler, IconId: "acme.icon.rocket");
                            _ = new CommandDescriptor("a.e", "x", handler, IconId: text);
                            _ = new CommandDescriptor("a.f", "x", handler, IconId: null);
                            _ = new CommandDescriptor("a.g", "x", handler);
                """),
            Id);
    }
}
