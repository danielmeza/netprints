using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>A copy of the HelloWorld sample whose generated file is stale, and a view of the generated files under it (FR-085).</summary>
internal static class GeneratedFileTree
{
    private const string GeneratedSuffix = ".g.cs";
    private const string BuildOutputFolder = "obj";

    /// <summary>
    /// Copies the sample next to a local SDK layout and edits its graph, so that regenerating would change the generated file.
    /// </summary>
    /// <returns>The path of the copied <c>.csproj</c>.</returns>
    public static string CreateStaleSample()
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        string directory = Path.GetDirectoryName(csproj) ?? throw new InvalidOperationException("The sample has no directory.");
        LocalSdkLayout.Write(directory);
        string graph = Path.Combine(directory, "HelloWorld.Program.netpc.json");
        File.WriteAllText(graph, File.ReadAllText(graph).Replace("Hello, World!", "Hello, stale generated file!", StringComparison.Ordinal));
        return csproj;
    }

    /// <summary>The generated files under <paramref name="directory"/> (outside <c>obj</c>) with their contents, by relative path.</summary>
    /// <param name="directory">The project folder.</param>
    /// <returns>The files.</returns>
    public static Dictionary<string, string> Read(string directory) =>
        Directory.EnumerateFiles(directory, "*" + GeneratedSuffix, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path))
            .Where(relative => !relative.StartsWith(BuildOutputFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToDictionary(relative => relative, relative => File.ReadAllText(Path.Combine(directory, relative)), StringComparer.Ordinal);
}
