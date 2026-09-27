using NetPrints.Core;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests;

/// <summary>Helpers for temporary directories and the checked-in sample.</summary>
public static class TestPaths
{
    public static string CreateTempDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "netprints-editor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Copies the checked-in <c>samples/HelloWorld</c> (linked into the test output) to a new temp
    /// directory (T059/T062a switched the editor to <c>.csproj</c>, research.md R21).
    /// </summary>
    /// <returns>The copied project's <c>.csproj</c> path.</returns>
    public static string CopyHelloWorldSample()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
        string target = CreateTempDirectory();
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return Path.Combine(target, "HelloWorld.csproj");
    }

    /// <summary>
    /// Copies <see cref="CopyHelloWorldSample"/> and loads it through a real
    /// <see cref="ProjectPersistence"/> (a <see cref="FakeProjectSystem"/> supplies the snapshot by
    /// scanning the copied files, no real MSBuild involved).
    /// </summary>
    public static async Task<Project> LoadHelloWorldCopyAsync(CancellationToken cancellationToken)
    {
        string csprojPath = CopyHelloWorldSample();
        ProjectPersistence persistence = TestEditor.CreatePersistence(new FakeProjectSystem());

        ProjectLoadResult loaded = await persistence.LoadAsync(csprojPath, cancellationToken);
        Assert.Empty(loaded.Issues);
        return loaded.Project;
    }

    public static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
    }
}
