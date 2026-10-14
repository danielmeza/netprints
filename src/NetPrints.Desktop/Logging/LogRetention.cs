using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NetPrints.Desktop.Logging;

/// <summary>The start-up sweep that keeps the newest session files (ADR-0033).</summary>
internal static partial class LogRetention
{
    /// <summary>Deletes the session files of a folder beyond the newest <paramref name="keep"/>; files it did not name are never touched.</summary>
    /// <param name="directory">The log folder; a missing folder is a no-op.</param>
    /// <param name="keep">How many of the newest session files stay.</param>
    /// <returns>The number of files deleted; a file that cannot be deleted is left and not counted.</returns>
    public static int Sweep(string directory, int keep)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(keep);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int deleted = 0;
        foreach (string path in Directory.EnumerateFiles(directory, SessionLog.FileNameGlob)
                     .Where(path => SessionFileName().IsMatch(Path.GetFileName(path)))
                     .OrderByDescending(path => path, StringComparer.Ordinal)
                     .Skip(keep))
        {
            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file in use or read-only stays; the next start tries again.
            }
        }

        return deleted;
    }

    [GeneratedRegex(@"^netprints-\d{8}-\d{6}-\d+\.log$", RegexOptions.CultureInvariant)]
    private static partial Regex SessionFileName();
}
