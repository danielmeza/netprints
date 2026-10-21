using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Localization;

/// <summary>NPA041: a satellite resx has a key the neutral one lacks, or a <c>{n}</c> placeholder count differs.</summary>
public class Npa041Tests
{
    private const string Id = "NPA041";

    private const string Source = "namespace Sample; public sealed class Empty;";

    [Fact]
    public async Task ASatelliteWithTheSameKeysAndPlaceholdersIsQuiet()
    {
        string neutral = ResxSamples.Resx(("A", "Save {0} of {1}"), ("B", "Close"));
        string satellite = ResxSamples.Resx(("A", "Guardar {1} de {0}"), ("B", "Cerrar"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(Source, [(ResxSamples.NeutralPath, neutral), (ResxSamples.SatellitePath, satellite)], Id);

        Assert.Empty(found);
    }

    [Fact]
    public async Task AKeyOnlyTheSatelliteHasIsReportedInTheSatelliteOnItsLine()
    {
        string neutral = ResxSamples.Resx(("A", "Save"));
        string satellite = ResxSamples.Resx(("A", "Guardar"), ("Extra", "Sobra"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(Source, [(ResxSamples.NeutralPath, neutral), (ResxSamples.SatellitePath, satellite)], Id);

        Diagnostic diagnostic = Assert.Single(found);
        Assert.Equal(ResxSamples.SatellitePath, diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(3, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Contains("Extra", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADifferentPlaceholderCountIsReported()
    {
        string neutral = ResxSamples.Resx(("A", "Save {0} of {1}"));
        string satellite = ResxSamples.Resx(("A", "Guardar {0}"));

        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(Source, [(ResxSamples.NeutralPath, neutral), (ResxSamples.SatellitePath, satellite)], Id);

        Diagnostic diagnostic = Assert.Single(found);
        Assert.Contains("placeholder", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASatelliteWithoutANeutralFileIsQuiet()
    {
        ImmutableArray<Diagnostic> found = await PinAnalyzerHarness.AnalyzeWithResxAsync(Source, [(ResxSamples.SatellitePath, ResxSamples.Resx(("A", "Guardar")))], Id);

        Assert.Empty(found);
    }

    [Fact]
    public void TheRuleIsAnErrorWithAGuideAnchor()
    {
        DiagnosticDescriptor descriptor = PinAnalyzerHarness.Descriptor(Id);

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.EndsWith("#npa041", descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }
}
