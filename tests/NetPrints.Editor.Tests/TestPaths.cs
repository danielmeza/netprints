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

    /// <summary>Copies samples/HelloWorld (linked into the test output) to a new temp directory.</summary>
    public static string CopyHelloWorldSample()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
        string target = CreateTempDirectory();
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return Path.Combine(target, "HelloWorld.netpp");
    }

    public static Project LoadHelloWorldCopy() => Project.LoadFromPath(CopyHelloWorldSample());

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
