using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using NetPrints.Catalog.Tests.Generator;
using NetPrints.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests.Runtime;

/// <summary>
/// AN-T09: the embedded catalogs of an assembly, read without loading it (metadata) and from a loaded assembly, are the same documents.
/// The assemblies are compiled in the test with the generator, because <c>CatalogFixtureLib</c> carries no embedded catalog (E1).
/// </summary>
public sealed class EmbeddedCatalogReaderTests : IDisposable
{
    private const string Requests =
        "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", AccessorName = \"Fixture\")]\n" +
        "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", Id = \"catalogfixturelib-flags\", Profile = \"fixture-flags.npprofile.json\", AccessorName = \"FixtureFlags\")]";

    private readonly string directory = Directory.CreateTempSubdirectory("np-embedded-").FullName;

    private readonly AssemblyLoadContext context = new("np-embedded-tests", isCollectible: true);

    public void Dispose()
    {
        context.Unload();
        Directory.Delete(directory, recursive: true);
    }

    private string EmitAssembly(string name, Microsoft.CodeAnalysis.Compilation output)
    {
        string path = Path.Combine(directory, name + ".dll");
        File.WriteAllBytes(path, GeneratorTestHost.Emit(output));
        return path;
    }

    private string TwoCatalogAssembly()
    {
        var (output, _) = GeneratorTestHost.Run(GeneratorFixtures.CompileConsumer(Requests), GeneratorFixtures.FixtureInputs());
        return EmitAssembly("Consumer", output);
    }

    private static string[] Serialize(IEnumerable<CatalogDocument> documents) => [.. documents.Select(CanonicalCatalogWriter.Write)];

    [Fact]
    public void AMetadataOnlyReadReturnsTheEmbeddedCatalogsInAttributeOrder()
    {
        IReadOnlyList<CatalogDocument> documents = EmbeddedCatalogReader.Read(TwoCatalogAssembly());

        Assert.Equal([FixtureCatalog.FixtureId, FixtureCatalog.FlagsId], documents.Select(d => d.Id).Order(StringComparer.Ordinal));
        Assert.Equal(
            File.ReadAllText(TestPaths.SnapshotPath("public-api.npcat.json")),
            CanonicalCatalogWriter.Write(documents.Single(d => d.Id == FixtureCatalog.FixtureId)));
        Assert.Equal(
            File.ReadAllText(TestPaths.SnapshotPath("fixture-flags.npcat.json")),
            CanonicalCatalogWriter.Write(documents.Single(d => d.Id == FixtureCatalog.FlagsId)));
    }

    [Fact]
    public void TheMetadataOnlyReadAndTheLoadedAssemblyReadGiveEqualDocuments()
    {
        string path = TwoCatalogAssembly();
        Assembly loaded = context.LoadFromAssemblyPath(path);

        string[] fromMetadata = Serialize(EmbeddedCatalogReader.Read(path));
        string[] fromAssembly = Serialize(EmbeddedCatalogReader.Read(loaded));

        Assert.Equal(2, fromMetadata.Length);
        Assert.Equal(fromMetadata, fromAssembly);
    }

    [Fact]
    public void TheMetadataOnlyReadDoesNotLoadTheAssembly()
    {
        string path = TwoCatalogAssembly();

        EmbeddedCatalogReader.Read(path);

        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Consumer");
    }

    [Fact]
    public void AnAssemblyWithoutCatalogsReadsAsEmpty()
    {
        string path = EmitAssembly("Plain", GeneratorTestHost.Compile("Plain", "public class Plain { }"));
        Assembly loaded = context.LoadFromAssemblyPath(path);

        Assert.Empty(EmbeddedCatalogReader.Read(path));
        Assert.Empty(EmbeddedCatalogReader.Read(loaded));
    }

    [Fact]
    public void ANativeFileReadsAsEmpty()
    {
        string path = Path.Combine(directory, "notes.txt");
        File.WriteAllText(path, "not an assembly");

        Assert.Empty(EmbeddedCatalogReader.Read(path));
    }

    [Fact]
    public void AMissingFileThrows() =>
        Assert.Throws<FileNotFoundException>(() => EmbeddedCatalogReader.Read(Path.Combine(directory, "missing.dll")));

    [Fact]
    public void ANewerSchemaThrowsACatalogFormatException()
    {
        var (output, _) = GeneratorTestHost.Run(
            GeneratorTestHost.Compile("Future", "[assembly: NetPrints.Annotations.NetPrintsEmbeddedCatalog(\"catalogfuture\", 2, \"{ \\\"schemaVersion\\\": 2 }\")]"));
        string path = EmitAssembly("Future", output);

        Assert.Throws<CatalogFormatException>(() => EmbeddedCatalogReader.Read(path));
    }

    [Fact]
    public void LoadEmbeddedCreatesATypeCatalogPerEmbeddedDocument()
    {
        Assembly loaded = context.LoadFromAssemblyPath(TwoCatalogAssembly());

        IReadOnlyList<ITypeCatalog> catalogs = CatalogLoader.LoadEmbedded(loaded);

        Assert.Equal([FixtureCatalog.FixtureId, FixtureCatalog.FlagsId], catalogs.Select(c => c.Info.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void LoadEmbeddedOfAnAssemblyWithoutCatalogsIsEmpty()
    {
        string path = EmitAssembly("Plain", GeneratorTestHost.Compile("Plain", "public class Plain { }"));

        Assert.Empty(CatalogLoader.LoadEmbedded(context.LoadFromAssemblyPath(path)));
    }
}
