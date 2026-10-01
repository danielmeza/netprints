using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T04 to AN-T06: the generator reports NPC001 to NPC006 and emits nothing for a failed catalog.</summary>
public sealed class DiagnosticsTests
{
    private const string Request = "[assembly: NetPrints.Annotations.NetPrintsCatalog(";

    private const string SiteBase = "https://danielmeza.github.io/netprints/";

    private static (Microsoft.CodeAnalysis.Compilation Output, ImmutableArray<Diagnostic> Diagnostics) Run(string source, GeneratorInputs? inputs = null) =>
        GeneratorTestHost.Run(GeneratorFixtures.CompileConsumer(source), inputs ?? GeneratorFixtures.FixtureInputs());

    private static void AssertMapsToADiagnosticsHeading(string url)
    {
        Assert.StartsWith(SiteBase, url, System.StringComparison.Ordinal);
        string[] parts = url[SiteBase.Length..].Split('#');
        string page = Path.Combine(TestPaths.RepositoryRoot(), "docs", parts[0] + ".md");

        Assert.True(File.Exists(page), $"{url} must map to an existing docs page, but {page} does not exist");
        Assert.Equal("diagnostics", parts[1]);
        Assert.Contains(File.ReadLines(page), line => line.Trim() == "## Diagnostics");
    }

    private static Diagnostic Single(ImmutableArray<Diagnostic> diagnostics, string id) => Assert.Single(diagnostics, d => d.Id == id);

    [Fact]
    public void AnUnreferencedAssemblyIsNpc001AndNothingIsEmitted()
    {
        var (output, diagnostics) = Run(Request + "\"Not.Referenced\")]");

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.UnreferencedAssembly);

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Not.Referenced", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Equal("NetPrints.Catalog", diagnostic.Descriptor.Category);
        AssertMapsToADiagnosticsHeading(diagnostic.Descriptor.HelpLinkUri);
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

    [Fact]
    public void EveryDescriptorAndBothPackageReadmesLinkToAnExistingDocsPage()
    {
        var (_, diagnostics) = Run(Request + "\"Not.Referenced\")]");
        AssertMapsToADiagnosticsHeading(Single(diagnostics, CatalogDiagnosticCodes.UnreferencedAssembly).Descriptor.HelpLinkUri);

        foreach (string project in new[] { "NetPrints.Annotations", "NetPrints.Catalog" })
        {
            string readme = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot(), "src", project, "README.md"));
            foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(readme, @"\((https://danielmeza\.github\.io/netprints/[^)]*)\)"))
            {
                string url = link.Groups[1].Value;
                string page = Path.Combine(TestPaths.RepositoryRoot(), "docs", url[SiteBase.Length..].Split('#')[0].TrimEnd('/') + ".md");
                Assert.True(File.Exists(page), $"{project} README links to {url}, which maps to {page}");
            }
        }
    }

    [Fact]
    public void AnAnnotatedLibraryBuiltWithoutDocumentationCommentsIsNpc007AndStillEmitsItsCatalog()
    {
        CSharpCompilation compilation = WithoutDocumentation(GeneratorFixtures.CompileFixtureSources());

        var (output, diagnostics) = GeneratorTestHost.Run(compilation);

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.MissingDocumentation);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("GenerateDocumentationFile", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        AssertMapsToADiagnosticsHeading(diagnostic.Descriptor.HelpLinkUri);
        Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void AnAnnotatedLibraryBuiltWithDocumentationCommentsHasNoNpc007()
    {
        var (_, diagnostics) = GeneratorTestHost.Run(GeneratorFixtures.CompileFixtureSources());

        Assert.DoesNotContain(diagnostics, d => d.Id == CatalogDiagnosticCodes.MissingDocumentation);
    }

    [Fact]
    public void ACompilationWithoutAnnotationsAndWithoutDocumentationCommentsHasNoNpc007()
    {
        var (_, diagnostics) = GeneratorTestHost.Run(WithoutDocumentation(GeneratorTestHost.Compile("Plain", "public class Plain { }")));

        Assert.DoesNotContain(diagnostics, d => d.Id == CatalogDiagnosticCodes.MissingDocumentation);
    }

    [Fact]
    public void AnInvalidAccessorNameIsNpc003AndNothingIsEmitted()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", AccessorName = \"not valid\")]");

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.InvalidProfileFile);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("not valid", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
    }

    [Fact]
    public void TwoIdsThatDeriveTheSameAccessorNameAreNpc006AndNeitherIsEmitted()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", Id = \"my-lib\")]\n" + Request + "\"CatalogFixtureLib\", Id = \"my.lib\")]");

        Diagnostic diagnostic = Single(diagnostics, CatalogDiagnosticCodes.DuplicateBuildCatalogId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("MyLib", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
        Assert.Empty(GeneratorTestHost.Errors(output));
    }

    [Fact]
    public void TwoExplicitAccessorNamesThatCollideAreNpc006()
    {
        var (output, diagnostics) = Run(Request + "\"CatalogFixtureLib\", Id = \"one\", AccessorName = \"Same\")]\n" + Request + "\"CatalogFixtureLib\", Id = \"two\", AccessorName = \"Same\")]");

        Assert.Single(diagnostics, d => d.Id == CatalogDiagnosticCodes.DuplicateBuildCatalogId);
        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
        Assert.Empty(GeneratorTestHost.Errors(output));
    }

    private static CSharpCompilation WithoutDocumentation(CSharpCompilation compilation)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            var options = (CSharpParseOptions)tree.Options;
            compilation = compilation.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(tree.GetRoot(), options.WithDocumentationMode(DocumentationMode.None)));
        }

        return compilation;
    }
}
