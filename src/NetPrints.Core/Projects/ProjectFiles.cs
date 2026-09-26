#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Projects;

/// <summary>
/// Files <see cref="IProjectSystem.CreateAsync"/> and legacy conversion write next to a project other
/// than the <c>.csproj</c> itself (project-system.md §1.1).
/// </summary>
public static class ProjectFiles
{
    /// <summary>File name of the <c>.gitattributes</c> file written next to a project.</summary>
    public const string GitAttributesFileName = ".gitattributes";

    /// <summary>
    /// The lines <see cref="EnsureGitAttributesAsync"/> guarantees are present in a project's
    /// <c>.gitattributes</c>, in order.
    /// </summary>
    public static IReadOnlyList<string> GitAttributesLines { get; } =
    [
        "*.netpc.json text eol=lf",
        "*.netpc.g.cs text eol=lf",
    ];

    /// <summary>
    /// Ensures <paramref name="directory"/>'s <c>.gitattributes</c> contains every line of
    /// <see cref="GitAttributesLines"/> (project-system.md §1.1): if the file does not exist, it is
    /// created with exactly those lines; if it exists, each missing line is appended (with a leading
    /// <c>\n</c> first if the file does not already end with one), and every existing line is left
    /// exactly as it was — never reordered or rewritten, and a line already present (compared trimmed)
    /// is not duplicated. The file is written whether or not <paramref name="directory"/> is a git
    /// repository.
    /// </summary>
    /// <param name="directory">Directory the <c>.gitattributes</c> file lives, or will be created,
    /// in.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file's previous content, or <see langword="null"/> if it did not exist yet — a
    /// caller that fails partway through a larger operation can restore this to roll the file back.</returns>
    public static async Task<byte[]?> EnsureGitAttributesAsync(string directory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        string path = Path.Combine(directory, GitAttributesFileName);
        byte[]? previousBytes = File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
        string existingText = previousBytes is null ? string.Empty : Encoding.UTF8.GetString(previousBytes);

        var existingLines = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in existingText.Split('\n'))
        {
            existingLines.Add(line.Trim());
        }

        var missingLines = new List<string>();
        foreach (string line in GitAttributesLines)
        {
            if (!existingLines.Contains(line))
            {
                missingLines.Add(line);
            }
        }

        if (missingLines.Count == 0 && previousBytes is not null)
        {
            return previousBytes;
        }

        var builder = new StringBuilder(existingText);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.Append('\n');
        }

        foreach (string line in missingLines)
        {
            builder.Append(line).Append('\n');
        }

        await WriteAtomicAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
        return previousBytes;
    }

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        string tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
