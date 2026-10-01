using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests;

public sealed class FixtureLibraryTests
{
    [Fact]
    public void LocatorFindsTheBuiltAssemblyAndItsDocumentation()
    {
        Assert.True(File.Exists(FixtureLibrary.AssemblyPath));
        Assert.True(File.Exists(FixtureLibrary.DocumentationPath));
        Assert.Equal("CatalogFixtureLib", Path.GetFileNameWithoutExtension(FixtureLibrary.AssemblyPath));
    }

    [Fact]
    public void TheFixtureCoversTheEdgeCases()
    {
        Assembly assembly = Assembly.LoadFile(FixtureLibrary.AssemblyPath);
        string[] names = assembly.GetExportedTypes().Select(t => t.FullName ?? t.Name).ToArray();

        Assert.Equal(new System.Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.Contains("Fixture.Generics.Box`1", names);
        Assert.Contains("Fixture.Generics.Box`1+Handle", names);
        Assert.Contains("Fixture.Geometry.Vector2", names);
        Assert.Contains("Fixture.Utilities.Helpers", names);
        Assert.Contains("Fixture.Legacy.Level", names);
        Assert.Contains("Fixture.Attributes.ExposeAttribute", names);
    }

    [Fact]
    public void ProfileFileIsCopiedNextToTheTests() =>
        Assert.True(File.Exists(FixtureLibrary.ProfilePath("fixture-flags.npprofile.json")));
}
