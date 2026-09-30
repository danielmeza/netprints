using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Finds <c>.netpc.json</c> graph files under a directory for the commands that take paths.</summary>
internal static class GraphFileSearch
{
    /// <summary>The extension of a graph file.</summary>
    public const string GraphExtension = ".netpc.json";

    private static readonly string[] SkippedDirectories = ["bin", "obj"];

    /// <summary>
    /// Adds the graphs under <paramref name="directory"/> to <paramref name="graphs"/>. Symbolic links and junctions are not followed (a link
    /// back up the tree would rescan it), <c>bin</c> and <c>obj</c> are skipped, and a directory that cannot be listed is reported and counted
    /// as a failure instead of aborting the walk.
    /// </summary>
    /// <param name="directory">The directory to search recursively.</param>
    /// <param name="graphs">Receives the full paths of the graphs found.</param>
    /// <param name="unreadable">Receives one line per directory that could not be listed.</param>
    /// <param name="relativeTo">The directory paths in <paramref name="unreadable"/> are shown relative to.</param>
    public static void Collect(string directory, ICollection<string> graphs, ICollection<string> unreadable, string relativeTo)
    {
        ArgumentNullException.ThrowIfNull(graphs);
        ArgumentNullException.ThrowIfNull(unreadable);
        string[] files;
        string[] children;
        try
        {
            files = Directory.GetFiles(directory, "*" + GraphExtension, SearchOption.TopDirectoryOnly);
            children = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            unreadable.Add($"{Path.GetRelativePath(relativeTo, directory)}: unreadable: {ex.Message}");
            return;
        }

        foreach (string file in files.Where(file => file.EndsWith(GraphExtension, StringComparison.OrdinalIgnoreCase)))
        {
            graphs.Add(file);
        }

        foreach (string child in children)
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)
                || new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            Collect(child, graphs, unreadable, relativeTo);
        }
    }
}
