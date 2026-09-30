using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T04 to AN-T06: the generator reports NPC001 to NPC006 and emits nothing for a failed catalog.</summary>
public sealed class DiagnosticsTests
{
    private const string Request = "[assembly: NetPrints.Annotations.NetPrintsCatalog(";

    private const string HelpLink = "https://danielmeza.github.io/netprints/docs/guide/catalogs#diagnostics";

    private static (Microsoft.CodeAnalysis.Compilation Output, ImmutableArray<Diagnostic> Diagnostics) Run(string source, GeneratorInputs? inputs = null) =>
        GeneratorTestHost.Run(GeneratorFixtures.CompileConsumer(source), inputs ?? GeneratorFixtures.FixtureInputs());

    private static Diagnostic Single(ImmutableArray<Diagnostic> diagnostics, string id) => Assert.Single(diagnostics, d => d.Id == id);

    [Fact]
    public void AnUnreferencedAssemblyIsNpc001AndNothingIsEmitted()
    {
        var (output, diagnostics) = Run(Request + "\"Not.Referenced\")]");

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.UnreferencedAssembly);

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Not.Referenced", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Equal("NetPrints.Catalog", diagnostic.Descriptor.Category);
        Assert.Equal(HelpLink, diagnostic.Descriptor.HelpLinkUri);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
        Assert.DoesNotContain(output.SyntaxTrees, t => t.FilePath.Contains("NetPrintsCatalog.", System.StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownProfileIsNpc002()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", Profile = \"does-not-exist\")]");

        Assert.Equal(DiagnosticSeverity.Error, Single(diagnostics, CatalogDiagnosticCodes.UnknownProfile).Severity);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void AnInvalidProfileFileIsNpc003()
    {
        GeneratorInputs inputs = GeneratorFixtures.FixtureInputs().AddFile("profiles/bad.npprofile.json", "{ \"id\": ");

        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", Profile = \"bad.npprofile.json\")]", inputs);

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.InvalidProfileFile);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("bad.npprofile.json", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void AProfileFileThatIsNotAnAdditionalFileIsNpc003()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", Profile = \"missing.npprofile.json\")]");

        Assert.Equal(DiagnosticSeverity.Error, Single(diagnostics, CatalogDiagnosticCodes.InvalidProfileFile).Severity);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void TwoCatalogsWithTheSameIdAreNpc006AndNeitherIsEmitted()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", AccessorName = \"One\")]\n" + Request + "\"CatalogFixtureLib\", Profile = \"annotated\", AccessorName = \"Two\")]");

        Assert.All(diagnostics.Where(d => d.Id == CatalogDiagnosticCodes.DuplicateBuildCatalogId), d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.NotEmpty(diagnostics.Where(d => d.Id == CatalogDiagnosticCodes.DuplicateBuildCatalogId));
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void ANodeOnAnInternalMethodIsNpc004AndTheMemberIsAbsent()
    {
        const string Source = """
            using NetPrints.Annotations;

            namespace Lib
            {
                public class Thing
                {
                    [NetPrintsNode]
                    public void Visible() { }

                    [NetPrintsNode]
                    internal void Hidden() { }
                }
            }
            """;

        var (output, diagnostics) = GeneratorTestHost.Run(GeneratorTestHost.Compile("Lib", Source));

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.IgnoredAnnotation);
        string json = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output)).Json;

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.NotEqual(Location.None, diagnostic.Location);
        Assert.Contains("Hidden", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("M:Lib.Thing.Visible", json, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden", json, System.StringComparison.Ordinal);
        Assert.Empty(GeneratorTestHost.Errors(output));
    }
}
