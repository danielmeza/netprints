using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T03: <c>[assembly: NetPrintsCatalog]</c> embeds the catalog of a referenced assembly, matching the tool's snapshots.</summary>
public sealed class ReferencedCatalogTests
{
    private const string PublicApiRequest = "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", AccessorName = \"Fixture\")]";

    private const string FlagsRequest =
        "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", Id = \"catalogfixturelib-flags\", Profile = \"fixture-flags.npprofile.json\", AccessorName = \"FixtureFlags\")]";

    private static (Microsoft.CodeAnalysis.Compilation Output, IReadOnlyList<Diagnostic> Diagnostics) Run(string source) =>
        Run(GeneratorFixtures.CompileConsumer(source));

    private static (Microsoft.CodeAnalysis.Compilation Output, IReadOnlyList<Diagnostic> Diagnostics) Run(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        var (output, diagnostics) = GeneratorTestHost.Run(compilation, GeneratorFixtures.FixtureInputs());
        return (output, diagnostics);
    }

    [Fact]
    public void ThePublicApiRequestProducesThePublicApiSnapshot()
    {
        var (output, diagnostics) = Run(PublicApiRequest);

        EmbeddedCatalog catalog = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output));

        Assert.Equal(FixtureCatalog.FixtureId, catalog.Id);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("public-api.npcat.json")), catalog.Json);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(GeneratorTestHost.Errors(output));
    }

    [Fact]
    public void AProfileFileFromAdditionalFilesProducesTheFlagsSnapshot()
    {
        var (output, diagnostics) = Run(FlagsRequest);

        EmbeddedCatalog catalog = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output));

        Assert.Equal(FixtureCatalog.FlagsId, catalog.Id);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("fixture-flags.npcat.json")), catalog.Json);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void TheAccessorMembersAreEmittedInTheRootNamespaceAndReturnTheJson()
    {
        var (output, _) = Run(PublicApiRequest + "\n" + FlagsRequest);

        INamedTypeSymbol accessors = output.GetTypeByMetadataName("Consumer.NetPrintsCatalogs") ?? throw new InvalidDataException("The accessor class is missing.");
        string[] names = [.. accessors.GetMembers().Select(m => m.Name)];

        Assert.Contains("Fixture", names);
        Assert.Contains("FixtureUtf8", names);
        Assert.Contains("FixtureFlags", names);
        Assert.Equal(2, GeneratorTestHost.EmbeddedCatalogs(output).Count);
        Assert.Empty(GeneratorTestHost.Errors(output));
    }

    [Fact]
    public void TheDefaultAccessorNameIsDerivedFromTheId()
    {
        var (output, _) = Run("[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", Id = \"fixture-lib.core\")]");

        INamedTypeSymbol accessors = output.GetTypeByMetadataName("Consumer.NetPrintsCatalogs") ?? throw new InvalidDataException("The accessor class is missing.");

        Assert.Contains(accessors.GetMembers(), m => m.Name == "FixtureLibCore");
    }

    [Fact]
    public void IncludeAndExcludeNarrowTheCatalogByTypeName()
    {
        var (output, _) = Run("[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", Include = new[] { \"Fixture.Geometry.*\" }, Exclude = new[] { \"Fixture.Geometry.Circle\" })]");

        string json = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output)).Json;

        Assert.Contains("\"id\": \"T:Fixture.Geometry.Vector2\"", json, System.StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\": \"T:Fixture.Geometry.Circle\"", json, System.StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\": \"T:Fixture.Utilities.Helpers\"", json, System.StringComparison.Ordinal);
    }
}
