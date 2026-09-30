using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T05: the built-in <c>annotated</c> profile, and research R11 (the injected attributes are visible on the compiled fixture).</summary>
public sealed class AnnotatedProfileTests
{
    private const string HelpersId = "T:Fixture.Utilities.Helpers";

    private const string CounterId = "T:Fixture.Utilities.Counter";

    private static readonly CatalogBuildResult Result = FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.Annotated, FixtureCatalog.FixtureId);

    [Fact]
    public void EqualsTheCommittedSnapshot() => FixtureCatalog.AssertSnapshot("annotated.npcat.json", Result);

    [Fact]
    public void InjectedMarkerAttributesAreVisibleOnTheCompiledFixture()
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture();
        INamedTypeSymbol helpers = assembly.GetTypeByMetadataName("Fixture.Utilities.Helpers")
            ?? throw new Xunit.Sdk.XunitException("Helpers not found");
        IMethodSymbol doubleMethod = helpers.GetMembers("Double").OfType<IMethodSymbol>().Single();

        string?[] names = [.. doubleMethod.GetAttributes().Select(a => a.AttributeClass?.ToDisplayString())];

        Assert.Contains("NetPrints.Annotations.NetPrintsNodeAttribute", names);
        Assert.NotNull(compilation);
    }

    [Fact]
    public void AnnotationsOnPublicMembersAreVisibleWithTheDefaultMetadataImport()
    {
        MetadataReference fixture = MetadataReference.CreateFromFile(FixtureLibrary.AssemblyPath);
        CSharpCompilation compilation = CSharpCompilation.Create("Tool", references: [.. FixtureCatalog.FrameworkReferences(), fixture]);
        IAssemblySymbol assembly = FixtureCatalog.AssemblyOf(compilation, fixture);

        CatalogBuildResult result = CatalogBuilder.Build(compilation, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated), FixtureCatalog.FixtureDocumentation(), new CatalogIdentity(FixtureCatalog.FixtureId));

        Assert.Equal(Result.Document.Types.Select(type => type.Id), result.Document.Types.Select(type => type.Id));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void SelectsAnnotatedTypesWithTheirPublicMembersAndAnnotatedMethodsOnly()
    {
        Assert.Equal([CounterId, HelpersId], Result.Document.Types.Select(type => type.Id));

        CatalogType helpers = FixtureCatalog.TypeOf(Result, HelpersId);
        Assert.Equal("Double", Assert.Single(helpers.Methods ?? []).Name);
        Assert.Null(helpers.Variables);
        Assert.Null(helpers.Node);

        CatalogType counter = FixtureCatalog.TypeOf(Result, CounterId);
        Assert.Equal(["Step"], counter.Methods?.Select(m => m.Name));
        Assert.Contains(counter.Variables ?? [], v => v.Name == "Count");
    }

    [Fact]
    public void CarriesNodeHintsAndDropsIgnoredMembers()
    {
        CatalogType counter = FixtureCatalog.TypeOf(Result, CounterId);
        Assert.Equal("Counter Widget", counter.Node?.DisplayName);
        Assert.Equal("Widgets", counter.Node?.Category);
        Assert.DoesNotContain(counter.Methods ?? [], m => m.Name == "Reset");
        Assert.DoesNotContain(counter.Variables ?? [], v => v.Name == "Secret");
        Assert.Equal(["increment"], counter.Methods?.Single(m => m.Name == "Step").Node?.Keywords);

        CatalogNodeHint doubleHint = FixtureCatalog.TypeOf(Result, HelpersId).Methods?.Single().Node
            ?? throw new Xunit.Sdk.XunitException("Double has no hint");
        Assert.Equal("Double", doubleHint.DisplayName);
        Assert.Equal("Math", doubleHint.Category);
        Assert.Equal(["multiply", "twice"], doubleHint.Keywords);
    }

    [Fact]
    public void WarnsAboutAnnotationsOnNonPublicMembers()
    {
        CatalogDiagnostic warning = Assert.Single(Result.Diagnostics);

        Assert.Equal(CatalogDiagnosticCodes.IgnoredAnnotation, warning.Code);
        Assert.Equal(CatalogDiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal("M:Fixture.Utilities.Helpers.Hidden", warning.Source);
        Assert.Contains("Hidden", warning.Message, System.StringComparison.Ordinal);
    }
}
