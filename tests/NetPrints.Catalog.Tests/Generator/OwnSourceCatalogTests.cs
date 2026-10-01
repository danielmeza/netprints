using System.IO;
using Microsoft.CodeAnalysis;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T02: the annotated symbols of the compilation's own sources are embedded as one catalog.</summary>
public sealed class OwnSourceCatalogTests
{
    [Fact]
    public void TheFixtureSourcesProduceTheAnnotatedSnapshot()
    {
        var (output, _) = GeneratorTestHost.Run(GeneratorFixtures.CompileFixtureSources());

        EmbeddedCatalog catalog = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output));

        Assert.Equal(FixtureCatalog.FixtureId, catalog.Id);
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("annotated.npcat.json")), catalog.Json);
        Assert.Empty(GeneratorTestHost.Errors(output));
    }

    [Fact]
    public void TheCatalogIsEmittedUnderTheSelfHintName()
    {
        var compilation = GeneratorFixtures.CompileFixtureSources();
        GeneratorDriver driver = GeneratorTestHost.CreateDriver(compilation).RunGenerators(compilation, TestContext.Current.CancellationToken);

        Assert.Contains(GeneratorTestHost.GeneratedSources(driver), s => s.HintName == "NetPrintsCatalog.Self.g.cs");
    }

    [Fact]
    public void TheAccessorIsEmittedInTheRootNamespace()
    {
        var (output, _) = GeneratorTestHost.Run(GeneratorFixtures.CompileFixtureSources(), new GeneratorInputs { RootNamespace = "Fixture" });

        INamedTypeSymbol accessors = output.GetTypeByMetadataName("Fixture.NetPrintsCatalogs") ?? throw new InvalidDataException("The accessor class is missing.");

        Assert.Contains(accessors.GetMembers(), m => m.Name == "Catalogfixturelib");
        Assert.Contains(accessors.GetMembers(), m => m.Name == "CatalogfixturelibUtf8");
    }

    [Fact]
    public void ACompilationWithoutAnnotationsEmbedsNothing()
    {
        var (output, diagnostics) = GeneratorTestHost.Run(GeneratorTestHost.Compile("Plain", "public class Plain { public void Do() { } }"));

        Assert.Empty(GeneratorTestHost.EmbeddedCatalogs(output));
        Assert.Empty(diagnostics);
    }
}
