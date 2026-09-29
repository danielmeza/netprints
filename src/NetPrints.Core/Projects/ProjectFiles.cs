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
    /// <returns>A task that completes when the file is up to date.</returns>
    public static async Task EnsureGitAttributesAsync(string directory, CancellationToken cancellationToken)
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
            return;
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

        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(builder.ToString());
        await WriteAtomicAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Full path of the generated <c>.netpc.g.cs</c> file next to a graph document (compilation-and-diagnostics.md
    /// §1: the key <see cref="NetPrints.Compilation.DiagnosticMapper.FromBuild"/> looks build messages up
    /// by), shared by <c>NetPrints.Serialization.ProjectPersistence</c> and the editor's live analysis
    /// host so both compute it the same way.
    /// </summary>
    /// <param name="graphFilePath">Full path of the graph document (ending in <c>.netpc.json</c>).</param>
    /// <returns><paramref name="graphFilePath"/> with its trailing <c>.json</c> replaced by <c>.g.cs</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="graphFilePath"/> does not end with
    /// <c>.json</c>.</exception>
    public static string GetGeneratedFilePath(string graphFilePath)
    {
        const string jsonSuffix = ".json";
        ArgumentException.ThrowIfNullOrEmpty(graphFilePath);

        return graphFilePath.EndsWith(jsonSuffix, StringComparison.OrdinalIgnoreCase)
            ? string.Concat(graphFilePath.AsSpan(0, graphFilePath.Length - jsonSuffix.Length), ".g.cs")
            : throw new ArgumentException($"Graph file '{graphFilePath}' does not end with '{jsonSuffix}'.", nameof(graphFilePath));
    }

    /// <summary>
    /// Writes <paramref name="path"/> atomically (R1-21 — the one place every atomic temp-file-plus-move
    /// write in the codebase goes through): <paramref name="writeToTempPath"/> fills a fresh temporary
    /// file next to <paramref name="path"/>, and only once it completes without throwing does the
    /// temporary file replace <paramref name="path"/> in one <see cref="File.Move(string, string, bool)"/>.
    /// On an exception (including cancellation) from <paramref name="writeToTempPath"/> or the move
    /// itself, the temporary file is deleted and <paramref name="path"/> is left untouched.
    /// </summary>
    /// <param name="path">Final path to write.</param>
    /// <param name="writeToTempPath">Writes the new content to the temporary path it is given.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="beforeMove">Runs immediately before the move, once <paramref name="writeToTempPath"/>
    /// has succeeded (a caller that must record a fact exactly at that boundary, such as
    /// <c>FileSystemDocumentStore</c> suppressing its own write's file system event).</param>
    public static async Task WriteAtomicAsync(
        string path, Func<string, CancellationToken, Task> writeToTempPath, CancellationToken cancellationToken, Action? beforeMove = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(writeToTempPath);

        string tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await writeToTempPath(tempPath, cancellationToken).ConfigureAwait(false);
            beforeMove?.Invoke();
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

    /// <summary>Convenience overload of <see cref="WriteAtomicAsync(string, Func{string, CancellationToken, Task}, CancellationToken, Action?)"/> that writes raw bytes.</summary>
    /// <param name="path">Final path to write.</param>
    /// <param name="bytes">Bytes to write.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public static Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken cancellationToken) =>
        WriteAtomicAsync(path, (tempPath, ct) => File.WriteAllBytesAsync(tempPath, bytes, ct), cancellationToken);
}
