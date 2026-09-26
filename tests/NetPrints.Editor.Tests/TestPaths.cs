using NetPrints.Core;

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
    /// Copies the legacy HelloWorld fixture (<c>tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld</c>,
    /// linked into the test output) to a new temp directory. The editor still opens <c>.netpp</c>
    /// (T059/T062a switches it to <c>samples/HelloWorld/HelloWorld.csproj</c>, research.md R21).
    /// </summary>
    public static string CopyHelloWorldSample()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "legacy-helloworld");
        string target = CreateTempDirectory();
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return Path.Combine(target, "HelloWorld.netpp");
    }

    public static Project LoadHelloWorldCopy()
    {
        var project = Project.LoadFromPath(CopyHelloWorldSample());
        Assert.NotNull(project);
        return project;
    }

    public static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
    }
}
