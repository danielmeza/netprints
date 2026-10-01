using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Catalog.Tests.Generator;
using Xunit;

namespace NetPrints.Catalog.Tests.EndToEnd;

/// <summary>
/// AN-T13 (SC-003): the tool flavor (the engine over metadata references), the driver flavor (the generator run in-process) and the built-assembly
/// flavor (a catalog read back from an assembly's metadata) give byte-identical catalogs for <c>public-api</c>, <c>annotated</c> and <c>fixture-flags</c>.
/// The built <c>public-api</c> and <c>fixture-flags</c> legs are the real <c>CatalogConsumerLib</c> build; <c>CatalogFixtureLib</c> itself embeds nothing
/// (E1), so the built <c>annotated</c> leg above is the fixture sources compiled with the generator and emitted to an assembly; the MSBuild-built <c>CatalogAnnotatedLib</c> is compared with the tool in its own test.
/// </summary>
public sealed class CrossFlavorSnapshotTests : System.IDisposable
{
    private const string RequestsSource =
        "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", AccessorName = \"Fixture\")]\n" +
        "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\", Id = \"catalogfixturelib-flags\", Profile = \"fixture-flags.npprofile.json\", AccessorName = \"FixtureFlags\")]";

    private readonly string directory = Directory.CreateTempSubdirectory("np-crossflavor-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static string Tool(CatalogProfile profile, string id) => CanonicalCatalogWriter.Write(FixtureCatalog.BuildFixture(profile, id).Document);

    private static IReadOnlyDictionary<string, string> Serialize(IEnumerable<CatalogDocument> documents) =>
        documents.ToDictionary(document => document.Id, CanonicalCatalogWriter.Write);

    private static IReadOnlyDictionary<string, string> BuiltConsumer() =>
        Serialize(EmbeddedCatalogReader.Read(FixtureLibrary.ConsumerAssemblyPath));

    private static IReadOnlyDictionary<string, string> DriverConsumer()
    {
        var (output, _) = GeneratorTestHost.Run(GeneratorFixtures.CompileConsumer(RequestsSource), GeneratorFixtures.FixtureInputs());
        return GeneratorTestHost.EmbeddedCatalogs(output).ToDictionary(catalog => catalog.Id, catalog => catalog.Json);
    }

    [Fact]
    public void PublicApiIsIdenticalInToolDriverAndBuiltAssembly()
    {
        string tool = Tool(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId);

        Assert.Equal(tool, DriverConsumer()[FixtureCatalog.FixtureId]);
        Assert.Equal(tool, BuiltConsumer()[FixtureCatalog.FixtureId]);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("public-api.npcat.json")), tool);
    }

    [Fact]
    public void FixtureFlagsIsIdenticalInToolDriverAndBuiltAssembly()
    {
        string tool = Tool(FixtureCatalog.FlagsProfile(), FixtureCatalog.FlagsId);

        Assert.Equal(tool, DriverConsumer()[FixtureCatalog.FlagsId]);
        Assert.Equal(tool, BuiltConsumer()[FixtureCatalog.FlagsId]);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("fixture-flags.npcat.json")), tool);
    }

    [Fact]
    public void AnnotatedIsIdenticalInToolDriverAndBuiltAssembly()
    {
        string tool = Tool(BuiltInCatalogProfiles.Annotated, FixtureCatalog.FixtureId);
        var (output, _) = GeneratorTestHost.Run(GeneratorFixtures.CompileFixtureSources());
        string driver = Assert.Single(GeneratorTestHost.EmbeddedCatalogs(output)).Json;
        string path = Path.Combine(directory, "CatalogFixtureLib.dll");
        File.WriteAllBytes(path, GeneratorTestHost.Emit(output));

        Assert.Equal(tool, driver);
        Assert.Equal(tool, Assert.Single(Serialize(EmbeddedCatalogReader.Read(path))).Value);
        Assert.Equal(File.ReadAllText(TestPaths.SnapshotPath("annotated.npcat.json")), tool);
    }

    [Fact]
    public void AnnotatedOverTheMsBuildBuiltAnnotatedLibraryIsIdenticalToItsEmbeddedCatalog()
    {
        MetadataReference library = MetadataReference.CreateFromFile(FixtureLibrary.AnnotatedAssemblyPath);
        CSharpCompilation compilation = FixtureCatalog.CreateCompilation("Tool", [.. FixtureCatalog.FrameworkReferences(), library]);
        var result = CatalogBuilder.Build(
            compilation,
            [FixtureCatalog.AssemblyOf(compilation, library)],
            new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated),
            XmlDocumentationSource.FromText(File.ReadAllText(FixtureLibrary.AnnotatedDocumentationPath)),
            new CatalogIdentity("catalogannotatedlib"));

        string embedded = Assert.Single(Serialize(EmbeddedCatalogReader.Read(FixtureLibrary.AnnotatedAssemblyPath))).Value;

        Assert.Equal(embedded, CanonicalCatalogWriter.Write(result.Document));
    }

    [Fact]
    public void TheConsumerLibraryEmbedsExactlyTheTwoRequestedCatalogs() =>
        Assert.Equal([FixtureCatalog.FixtureId, FixtureCatalog.FlagsId], BuiltConsumer().Keys.Order(System.StringComparer.Ordinal));
}
