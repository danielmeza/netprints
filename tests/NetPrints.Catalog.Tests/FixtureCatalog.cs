using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests;

/// <summary>Builds catalogs of the fixture library (or of in-memory sources) the way the tool does: over a compilation of metadata references.</summary>
internal static class FixtureCatalog
{
    public const string FixtureId = "catalogfixturelib";

    public const string FlagsId = "catalogfixturelib-flags";

    private static readonly ImmutableArray<string> FrameworkPaths = LoadFrameworkPaths();

    public static CSharpCompilation CreateCompilation(string assemblyName, IEnumerable<MetadataReference> references, params SyntaxTree[] trees) =>
        CSharpCompilation.Create(
            assemblyName,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, metadataImportOptions: MetadataImportOptions.All));

    public static IReadOnlyList<MetadataReference> FrameworkReferences() =>
        [.. FrameworkPaths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];

    public static (CSharpCompilation Compilation, IAssemblySymbol Assembly) OpenFixture(Func<IReadOnlyList<MetadataReference>, IEnumerable<MetadataReference>>? order = null)
    {
        MetadataReference fixture = MetadataReference.CreateFromFile(FixtureLibrary.AssemblyPath);
        IReadOnlyList<MetadataReference> references = [.. FrameworkReferences(), fixture];
        CSharpCompilation compilation = CreateCompilation("Tool", order is null ? references : order(references));
        return (compilation, AssemblyOf(compilation, fixture));
    }

    public static IAssemblySymbol AssemblyOf(Microsoft.CodeAnalysis.Compilation compilation, MetadataReference reference) =>
        compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol
        ?? throw new InvalidOperationException("The reference is not an assembly.");

    public static IDocumentationSource FixtureDocumentation() => XmlDocumentationSource.FromText(File.ReadAllText(FixtureLibrary.DocumentationPath));

    public static CatalogProfile FlagsProfile() => ProfileJson.Parse(File.ReadAllText(FixtureLibrary.ProfilePath("fixture-flags.npprofile.json")));

    public static CatalogBuildResult BuildFixture(CatalogProfile profile, string id) => BuildFixture(new CatalogProfileFilter(profile), id);

    public static CatalogBuildResult BuildFixture(ICatalogFilter filter, string id)
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = OpenFixture();
        return CatalogBuilder.Build(compilation, [assembly], filter, FixtureDocumentation(), new CatalogIdentity(id));
    }

    public static CatalogType TypeOf(CatalogBuildResult result, string documentationId) =>
        result.Document.Types.Single(type => type.Id == documentationId);

    public static void AssertSnapshot(string fileName, CatalogBuildResult result)
    {
        string actual = CanonicalCatalogWriter.Write(result.Document);
        string path = TestPaths.SnapshotPath(fileName);
        if (TestPaths.UpdateSnapshots)
        {
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"Missing snapshot {path}; regenerate with {TestPaths.UpdateSnapshotsVariable}=1");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    public static void AssertNoDiagnostics(CatalogBuildResult result) =>
        Assert.True(result.Diagnostics.Count == 0, string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code} {d.Message}")));

    private static ImmutableArray<string> LoadFrameworkPaths()
    {
        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        return
        [
            .. trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => Path.GetFileName(path) is { } name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll")),
        ];
    }
}
