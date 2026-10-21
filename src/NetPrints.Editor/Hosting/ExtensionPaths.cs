namespace NetPrints.Editor.Hosting;

/// <summary>Compares extension folders and files the way the file system does.</summary>
internal static class ExtensionPaths
{
    /// <summary>Gets the comparer for normalized paths: case-insensitive on Windows, ordinal elsewhere.</summary>
    internal static StringComparer Comparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Gets the full path without a trailing separator.</summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The full path, without a trailing directory separator.</returns>
    internal static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>Gets whether two paths name the same file or folder.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true"/> when both paths normalize to the same path.</returns>
    internal static bool Same(string left, string right) => Comparer.Equals(Normalize(left), Normalize(right));
}
