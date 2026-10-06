using System.Security.Cryptography;
using NetPrints.Editor.StartPage;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>Samples (FR-043): the bundled samples, their copy to a user-chosen folder, and the bundled files themselves.</summary>
public sealed class SampleCatalogTests : IDisposable
{
    // SHA-256 of the checked-in samples/HelloWorld files, with CRLF read as LF; a change to the sample changes these on purpose.
    private static readonly Dictionary<string, string> ExpectedHashes = new()
    {
        [".gitattributes"] = "a05c6e1751d148424a2761ec6ae57f2d08964a5324ab7c276471349870e53b59",
        ["HelloWorld.Program.netpc.g.cs"] = "eca35389a4e543221da3ea5c87d8c7a25912b620839b4f560fd38e00d2e52339",
        ["HelloWorld.Program.netpc.json"] = "e1dfb1309f66be1a055dff4f11f89f357489a7325e5656765edac7e8b5f4e8d4",
        ["HelloWorld.csproj"] = "28b120f3355cf3f54da07ae52d90f3cff0278851b800422963a65bc6a6fcd406",
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string TempRoot()
    {
        string root = TestPaths.CreateTempDirectory();
        cleanup.Add(root);
        return root;
    }

    private static string Hash(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).ReplaceLineEndings("\n"))));

    private static string SamplesInSource() => Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "samples", "HelloWorld");

    [Fact]
    public void TheBundledCatalogListsHelloWorld()
    {
        SampleDescriptor sample = Assert.Single(SampleCatalog.Bundled.Samples, candidate => candidate.Name == "HelloWorld");

        Assert.Equal("HelloWorld.csproj", sample.ProjectFileName);
        Assert.True(File.Exists(Path.Combine(sample.Directory, sample.ProjectFileName)));
    }

    [Fact]
    public void OnlyFoldersWithAProjectAreSamplesAndTheyAreListedByName()
    {
        string root = TempRoot();
        foreach (string name in new[] { "Zeta", "Alpha" })
        {
            Directory.CreateDirectory(Path.Combine(root, name));
            File.WriteAllText(Path.Combine(root, name, name + ".csproj"), "<Project />");
        }

        Directory.CreateDirectory(Path.Combine(root, "NotASample"));
        File.WriteAllText(Path.Combine(root, "loose.txt"), "x");

        Assert.Equal(["Alpha", "Zeta"], new SampleCatalog(root).Samples.Select(sample => sample.Name));
    }

    [Fact]
    public void ACatalogOverAMissingFolderIsEmpty() =>
        Assert.Empty(new SampleCatalog(Path.Combine(TempRoot(), "missing")).Samples);

    [Fact]
    public async Task CopyingWritesTheSamplesFilesButNotItsBuildOutputAndLeavesTheOriginalAlone()
    {
        string root = TempRoot();
        string source = Path.Combine(root, "samples", "Demo");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        Directory.CreateDirectory(Path.Combine(source, "bin"));
        Directory.CreateDirectory(Path.Combine(source, "obj"));
        Directory.CreateDirectory(Path.Combine(source, "Compiled_Demo"));
        File.WriteAllText(Path.Combine(source, "Demo.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(source, "sub", "Graph.netpc.json"), "{}");
        File.WriteAllText(Path.Combine(source, "bin", "a.dll"), "x");
        File.WriteAllText(Path.Combine(source, "obj", "b.json"), "x");
        File.WriteAllText(Path.Combine(source, "Compiled_Demo", "c.cs"), "x");
        var catalog = new SampleCatalog(Path.Combine(root, "samples"));
        string target = Path.Combine(root, "out", "Demo");

        string csproj = await catalog.CopyAsync(catalog.Samples.Single(), target, Token);

        Assert.Equal(Path.Combine(target, "Demo.csproj"), csproj);
        Assert.Equal(["Demo.csproj", Path.Combine("sub", "Graph.netpc.json")], Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(target, file)).Order());
        Assert.True(File.Exists(Path.Combine(source, "bin", "a.dll")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(source, "sub", "Graph.netpc.json")));
    }

    [Fact]
    public async Task CopyingIntoAFolderThatIsNotEmptyIsRejectedAndOverwritesNothing()
    {
        SampleCatalog catalog = SampleCatalog.Bundled;
        string target = Path.Combine(TempRoot(), "Busy");
        Directory.CreateDirectory(target);
        string existing = Path.Combine(target, "HelloWorld.csproj");
        await File.WriteAllTextAsync(existing, "mine", Token);

        await Assert.ThrowsAsync<IOException>(() => catalog.CopyAsync(catalog.Samples.Single(sample => sample.Name == "HelloWorld"), target, Token));

        Assert.Equal("mine", await File.ReadAllTextAsync(existing, Token));
        Assert.Single(Directory.GetFileSystemEntries(target));
    }

    [Fact]
    public void TheCheckedInHelloWorldFilesHaveNotChanged()
    {
        string folder = SamplesInSource();

        Assert.Equal(
            ExpectedHashes.Keys.Order(),
            Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>().Order());
        Assert.All(ExpectedHashes, entry => Assert.Equal(entry.Value, Hash(Path.Combine(folder, entry.Key))));
    }

    [Fact]
    public void TheDesktopProjectBundlesTheSamplesFilesUnchangedAndNothingElse()
    {
        string bundled = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "src", "NetPrints.Desktop", "bin", LocalSdkLayout.DetectConfiguration(), "net10.0", "samples", "HelloWorld");

        Assert.Equal(
            ExpectedHashes.Keys.Order(),
            Directory.GetFileSystemEntries(bundled).Select(Path.GetFileName).OfType<string>().Order());
        Assert.All(ExpectedHashes, entry => Assert.Equal(entry.Value, Hash(Path.Combine(bundled, entry.Key))));
    }

    [Fact]
    public async Task ACopyOfTheBundledSampleHasTheSameFiles()
    {
        SampleCatalog catalog = SampleCatalog.Bundled;
        string target = Path.Combine(TempRoot(), "HelloWorld");

        await catalog.CopyAsync(catalog.Samples.Single(sample => sample.Name == "HelloWorld"), target, Token);

        Assert.All(ExpectedHashes, entry => Assert.Equal(entry.Value, Hash(Path.Combine(target, entry.Key))));
    }
}
