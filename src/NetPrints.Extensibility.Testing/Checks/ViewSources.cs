namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>Finds the <c>.axaml</c> sources of an extension.</summary>
internal static class ViewSources
{
    private const string Pattern = "*.axaml";

    /// <summary>Lists the <c>.axaml</c> files under <paramref name="directory"/>, leaving out build output, in path order.</summary>
    /// <param name="directory">The directory to search.</param>
    /// <returns>The full paths.</returns>
    public static IEnumerable<string> Find(string directory)
    {
        string bin = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;
        string obj = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(directory, Pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains(bin, StringComparison.Ordinal) && !path.Contains(obj, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }
}
