using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Localization;

/// <summary>NPA042: a <c>Node.&lt;kind&gt;.*</c> resx key of a node kind in the project that no declaration uses.</summary>
public class Npa042Tests
{
    private const string Id = "NPA042";

    [Fact]
    public async Task EveryKeyADeclarationUsesIsQuiet()
    {
        string resx = ResxSamples.Resx(
            (ResxSamples.TitleKey, "Loop"),
            ("Node.sample.loop.Description", "Repeats"),
            (ResxSamples.ExecutionKey, "Execution"),
            (ResxSamples.LoopKey, "Loop body"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, resx)], Id);

        Assert.Empty(found);
    }

    [Fact]
    public async Task AKeyOfAPinTheKindNoLongerDeclaresIsReportedInTheResxOnItsLine()
    {
        string resx = ResxSamples.Resx((ResxSamples.TitleKey, "Loop"), ("Node.sample.loop.Pin.out.exec.Gone", "Gone"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, resx)], Id);

        Diagnostic diagnostic = Assert.Single(found);
        Assert.Equal(ResxSamples.NeutralPath, diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(3, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public async Task KeysOfOtherKindsAndOtherAreasAreLeftAlone()
    {
        string resx = ResxSamples.Resx((ResxSamples.TitleKey, "Loop"), ("Node.other.kind.Title", "Other"), ("Command.x.Label", "X"), ("Category.netprints", "NetPrints"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, resx)], Id);

        Assert.Empty(found);
    }

    [Fact]
    public void TheRuleIsInfoWithAGuideAnchor()
    {
        DiagnosticDescriptor descriptor = PinAnalyzerHarness.Descriptor(Id);

        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.EndsWith("#npa042", descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }
}
