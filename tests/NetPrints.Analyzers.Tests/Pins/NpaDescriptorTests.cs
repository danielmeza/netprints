using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>The contract of the NPA001-NPA009 descriptors (contracts/analyzer-diagnostics.md §3): errors, on by default, with a guide anchor.</summary>
public class NpaDescriptorTests
{
    public static TheoryData<string> Codes => new()
    {
        "NPA001",
        "NPA002",
        "NPA003",
        "NPA004",
        "NPA005",
        "NPA006",
        "NPA007",
        "NPA008",
        "NPA009",
    };

    [Fact]
    public void KeyTrackingRulesCarryTheirContractSeverities()
    {
        Assert.Equal(DiagnosticSeverity.Warning, PinAnalyzerHarness.Descriptor("NPA010").DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Error, PinAnalyzerHarness.Descriptor("NPA011").DefaultSeverity);
        Assert.EndsWith("#npa010", PinAnalyzerHarness.Descriptor("NPA010").HelpLinkUri);
        Assert.EndsWith("#npa011", PinAnalyzerHarness.Descriptor("NPA011").HelpLinkUri);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EachRuleIsAnErrorOnByDefaultWithAGuideAnchor(string id)
    {
        DiagnosticDescriptor descriptor = PinAnalyzerHarness.Descriptor(id);

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.EndsWith("#" + id.ToLowerInvariant(), descriptor.HelpLinkUri);
    }
}
