using System;
using System.IO;
using System.Linq;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Catalog.Tests.Sources;

/// <summary>T053: <see cref="CatalogCompilationFactory"/> turns resolved sources into the compilation, assembly symbols and documentation the builder needs.</summary>
public sealed class CatalogCompilationFactoryTests
{
    private static CatalogSourceSet FixtureSources(params ResolvedAssembly[] extraReferences) => new(
        [.. FixtureCatalog.FrameworkAssemblyPaths().Select(path => new ResolvedAssembly(path, null)), new ResolvedAssembly(FixtureLibrary.AssemblyPath, FixtureLibrary.DocumentationPath), .. extraReferences],
        [new ResolvedAssembly(FixtureLibrary.AssemblyPath, FixtureLibrary.DocumentationPath)],
        [],
        []);

    [Fact]
    public void BuildsACompilationWithoutSyntaxTreesOverTheReferences()
    {
        CatalogCompilationInput input = CatalogCompilationFactory.Create(FixtureSources());

        Assert.Empty(input.Compilation.SyntaxTrees);
        Assert.Equal("CatalogFixtureLib", Assert.Single(input.Assemblies).Name);
    }

    [Fact]
    public void TheBuilderProducesTheFixtureCatalogWithDocumentation()
    {
        CatalogCompilationInput input = CatalogCompilationFactory.Create(FixtureSources());

        CatalogBuildResult result = CatalogBuilder.Build(
            input.Compilation,
            input.Assemblies,
            new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi),
            input.Documentation,
            new CatalogIdentity(FixtureCatalog.FixtureId));

        Assert.Equal(FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId).Document.Types.Count, result.Document.Types.Count);
        Assert.Contains(result.Document.Types, type => type.Summary is not null);
    }

    [Fact]
    public void AnUnreadableAssemblyIsASourceError()
    {
        string missing = Path.Combine(Path.GetTempPath(), "np-missing", "Nope.dll");
        CatalogSourceSet sources = new([new ResolvedAssembly(missing, null)], [new ResolvedAssembly(missing, null)], [], []);

        CatalogSourceException exception = Assert.Throws<CatalogSourceException>(() => CatalogCompilationFactory.Create(sources));

        Assert.Contains("Nope.dll", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAnAssemblyIsASourceError()
    {
        string path = Path.Combine(Path.GetTempPath(), "np-notanassembly-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllText(path, "not a PE file");
        try
        {
            CatalogSourceSet sources = new([new ResolvedAssembly(path, null)], [new ResolvedAssembly(path, null)], [], []);

            Assert.Throws<CatalogSourceException>(() => CatalogCompilationFactory.Create(sources));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
