using System;

namespace NetPrints.Cli.Infrastructure;

/// <summary>How <c>generate --graph</c> matches a path against the project's graph files.</summary>
internal static class GraphPathComparison
{
    /// <summary>Gets the comparison of the running OS: case-insensitive on Windows and macOS, ordinal elsewhere.</summary>
    public static StringComparer Default { get; } = For(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());

    /// <summary>Gets the comparison for a file system.</summary>
    /// <param name="caseInsensitive">Whether the file system ignores case.</param>
    /// <returns>An ordinal comparer, ignoring case when <paramref name="caseInsensitive"/>.</returns>
    public static StringComparer For(bool caseInsensitive) => caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
