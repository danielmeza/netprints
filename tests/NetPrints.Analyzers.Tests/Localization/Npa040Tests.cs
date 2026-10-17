using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Localization;

/// <summary>NPA040: a convention resource key a node kind implies is missing from the neutral resx.</summary>
public class Npa040Tests
{
    private const string Id = "NPA040";

    [Fact]
    public async Task ACompleteNeutralResxIsQuiet()
    {
        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, ResxSamples.Complete())], Id);

        Assert.Empty(found);
    }

    [Fact]
    public async Task AMissingTitleKeyIsReportedOnTheClassOfAFixedTitle()
    {
        string resx = ResxSamples.Resx((ResxSamples.ExecutionKey, "Execution"), (ResxSamples.LoopKey, "Loop body"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.FixedTitleLoop(), [(ResxSamples.NeutralPath, resx)], Id);

        Diagnostic diagnostic = Assert.Single(found);
        Assert.Contains(ResxSamples.TitleKey, diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingTitleKeyIsQuietWhenTheTitleIsBuiltFromTheNode()
    {
        string resx = ResxSamples.Resx((ResxSamples.ExecutionKey, "Execution"), (ResxSamples.LoopKey, "Loop body"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, resx)], Id);

        Assert.Empty(found);
    }

    [Fact]
    public async Task AMissingPinKeyIsReportedOnThePinProperty()
    {
        string resx = ResxSamples.Resx((ResxSamples.TitleKey, "Loop"), (ResxSamples.ExecutionKey, "Execution"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.NeutralPath, resx)], Id);

        Diagnostic diagnostic = Assert.Single(found);
        Assert.Contains(ResxSamples.LoopKey, diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Equal("LoopPin", diagnostic.Location.SourceTree?.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task AProjectWithoutAResxIsQuiet()
    {
        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [], Id);

        Assert.Empty(found);
    }

    [Fact]
    public async Task ASatelliteAloneDoesNotCountAsTheNeutralResx()
    {
        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(ResxSamples.Loop(), [(ResxSamples.SatellitePath, ResxSamples.Complete())], Id);

        Assert.Empty(found);
    }

    [Fact]
    public void TheRuleIsAWarningWithAGuideAnchor()
    {
        DiagnosticDescriptor descriptor = PinAnalyzerHarness.Descriptor(Id);

        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.EndsWith("#npa040", descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }
}
