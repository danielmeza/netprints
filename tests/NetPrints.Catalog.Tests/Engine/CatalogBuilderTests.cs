using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>Identity defaults, argument checks, custom filters and the built-in profile table.</summary>
public sealed class CatalogBuilderTests
{
    private sealed class OnlyDoubleFilter : ICatalogFilter
    {
        public string ProfileId => "only-double";

        public bool IncludeType(INamedTypeSymbol type) => type.Name == "Helpers";

        public bool IncludeMember(ISymbol member) => member.Name == "Double";

        public CatalogNodeHint? DescribeNode(ISymbol symbol) => symbol.Name == "Double" ? new CatalogNodeHint { DisplayName = "Twice" } : null;
    }

    [Fact]
    public void TheIdentityDefaultsToTheFirstAssemblyLowerCasedAndItsVersion()
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture();

        CatalogBuildResult result = CatalogBuilder.Build(compilation, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated), XmlDocumentationSource.Empty, new CatalogIdentity());

        Assert.Equal("catalogfixturelib", result.Document.Id);
        Assert.Equal("1.0.0.0", result.Document.Version);
        Assert.Equal("annotated", result.Document.Profile);
    }

    [Theory]
    [InlineData("_Private", "private")]
    [InlineData("My Lib", "my-lib")]
    [InlineData("Acme.Core_2", "acme.core_2")]
    [InlineData("Ünï", "n")]
    [InlineData("___", "catalog")]
    public void ADerivedIdIsMadeValidFromTheAssemblyName(string assemblyName, string expected)
    {
        CSharpCompilation library = FixtureCatalog.CreateCompilation(assemblyName, FixtureCatalog.FrameworkReferences(), CSharpSyntaxTree.ParseText("public class C { }", cancellationToken: TestContext.Current.CancellationToken));
        using System.IO.MemoryStream stream = new();
        Assert.True(library.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Success);
        MetadataReference reference = MetadataReference.CreateFromStream(new System.IO.MemoryStream(stream.ToArray()));
        CSharpCompilation tool = FixtureCatalog.CreateCompilation("Tool", [.. FixtureCatalog.FrameworkReferences(), reference]);

        CatalogBuildResult result = CatalogBuilder.Build(tool, [FixtureCatalog.AssemblyOf(tool, reference)], new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi), XmlDocumentationSource.Empty, new CatalogIdentity());

        Assert.Equal(expected, result.Document.Id);
        Assert.True(CatalogIdentity.IsValidId(result.Document.Id));
    }

    [Fact]
    public void AnExplicitIdentityOverridesTheDefaults()
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture();

        CatalogBuildResult result = CatalogBuilder.Build(compilation, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated), XmlDocumentationSource.Empty, new CatalogIdentity("my-id.1", "9.9.9"));

        Assert.Equal("my-id.1", result.Document.Id);
        Assert.Equal("9.9.9", result.Document.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("-lead")]
    [InlineData("has space")]
    public void AnInvalidCatalogIdIsRejected(string id)
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture();

        Assert.Throws<ArgumentException>(() => CatalogBuilder.Build(compilation, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated), XmlDocumentationSource.Empty, new CatalogIdentity(id)));
    }

    [Fact]
    public void RequiresAtLeastOneAssembly()
    {
        (CSharpCompilation compilation, _) = FixtureCatalog.OpenFixture();
        CatalogProfileFilter filter = new(BuiltInCatalogProfiles.PublicApi);

        Assert.Throws<ArgumentException>(() => CatalogBuilder.Build(compilation, [], filter, XmlDocumentationSource.Empty, new CatalogIdentity()));
    }

    [Fact]
    public void ACustomFilterSelectsSymbolsAndDescribesNodes()
    {
        CatalogBuildResult result = FixtureCatalog.BuildFixture(new OnlyDoubleFilter(), "only-double");

        CatalogType helpers = Assert.Single(result.Document.Types);
        CatalogMethod method = Assert.Single(helpers.Methods ?? []);
        Assert.Equal("Double", method.Name);
        Assert.Equal("Twice", method.Node?.DisplayName);
        Assert.Equal("only-double", result.Document.Profile);
        Assert.Null(helpers.Variables);
    }

    [Fact]
    public void BuiltInProfilesAreLookedUpById()
    {
        Assert.Equal([CatalogProfile.PublicApiId, CatalogProfile.AnnotatedId], BuiltInCatalogProfiles.Ids);
        Assert.Same(BuiltInCatalogProfiles.PublicApi, BuiltInCatalogProfiles.TryGet("public-api"));
        Assert.Same(BuiltInCatalogProfiles.Annotated, BuiltInCatalogProfiles.TryGet("annotated"));
        Assert.Null(BuiltInCatalogProfiles.TryGet("Public-Api"));
        Assert.Null(BuiltInCatalogProfiles.TryGet("fixture-flags"));
        Assert.Equal(CatalogProfileBase.PublicApi, BuiltInCatalogProfiles.PublicApi.Base);
        Assert.Equal(CatalogProfileBase.Annotated, BuiltInCatalogProfiles.Annotated.Base);
    }

    [Fact]
    public void SymbolIdsAreDocumentationCommentIds()
    {
        (_, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture();
        INamedTypeSymbol handle = assembly.GetTypeByMetadataName("Fixture.Generics.Box`1+Handle") ?? throw new Xunit.Sdk.XunitException("Handle missing");

        Assert.Equal("T:Fixture.Generics.Box`1.Handle", SymbolIds.Of(handle));
        Assert.Equal("Fixture.Generics.Box`1.Handle", SymbolIds.FullName(handle));
        Assert.Equal("M:Fixture.Utilities.Helpers.TryParse(System.String,System.Int32@)", SymbolIds.Of(assembly.GetTypeByMetadataName("Fixture.Utilities.Helpers")?.GetMembers("TryParse").Single() ?? throw new Xunit.Sdk.XunitException("TryParse missing")));
    }
}
