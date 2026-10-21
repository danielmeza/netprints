using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA024: two commands share a menu id, group and order in one compilation.</summary>
public class Npa024Tests
{
    private const string Id = "NPA024";

    [Fact]
    public async Task TheSecondCommandInAMenuSlotIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.one", "x", handler, Menu: new MenuPlacement("File", "save", 0));
                            _ = new CommandDescriptor("a.two", "x", handler, Menu: {|NPA024:new MenuPlacement("File", "save", 0)|});
                """),
            Id);
    }

    [Fact]
    public async Task ADifferentMenuGroupOrOrderIsAccepted()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.one", "x", handler, Menu: new MenuPlacement("File", "save", 0));
                            _ = new CommandDescriptor("a.two", "x", handler, Menu: new MenuPlacement("Edit", "save", 0));
                            _ = new CommandDescriptor("a.three", "x", handler, Menu: new MenuPlacement("File", "open", 0));
                            _ = new CommandDescriptor("a.four", "x", handler, Menu: new MenuPlacement("File", "save", 1));
                            _ = new CommandDescriptor("a.five", "x", handler, Menu: new MenuPlacement(text, "save", 1));
                            _ = new CommandDescriptor("a.six", "x", handler, Menu: new MenuPlacement(text, "save", 1));
                """),
            Id);
    }

    [Fact]
    public void TheRuleIsAWarning()
    {
        DiagnosticDescriptor descriptor = PinAnalyzerHarness.Descriptor(Id);

        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.EndsWith("#npa024", descriptor.HelpLinkUri);
    }
}
