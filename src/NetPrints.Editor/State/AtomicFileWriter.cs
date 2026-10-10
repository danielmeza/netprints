namespace NetPrints.Editor.State;

/// <summary>
/// Writes the editor's per-user files atomically (state-files.md §1): the content goes to <c>&lt;file&gt;.&lt;random&gt;.tmp</c> in the same
/// folder and is then renamed over the target, so a crash leaves the old file or the new one, never half of one.
/// </summary>
/// <param name="fileSystem">The file system to write to.</param>
/// <param name="time">The clock that dates stray temporary files; the system clock when null.</param>
public sealed class AtomicFileWriter(IEditorFileSystem fileSystem, TimeProvider? time = null)
{
    private const string TempSuffix = ".tmp";
    private static readonly TimeSpan StrayAge = TimeSpan.FromDays(1);

    private readonly TimeProvider time = time ?? TimeProvider.System;

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/>, creating its folder when it is missing.</summary>
    /// <param name="path">The target file.</param>
    /// <param name="bytes">The new content.</param>
    public void Write(string path, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(bytes);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !fileSystem.DirectoryExists(directory))
        {
            fileSystem.CreateDirectory(directory);
        }

        DeleteStrayTempFiles(path, directory);
        string temp = $"{path}.{Guid.NewGuid():N}{TempSuffix}";
        try
        {
            fileSystem.WriteAllBytes(temp, bytes);
            fileSystem.Move(temp, path);
        }
        catch
        {
            fileSystem.DeleteFile(temp);
            throw;
        }
    }

    private void DeleteStrayTempFiles(string path, string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        string prefix = Path.GetFileName(path) + ".";
        DateTime cutoff = time.GetUtcNow().UtcDateTime - StrayAge;
        try
        {
            foreach (string file in fileSystem.EnumerateFiles(directory))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(TempSuffix, StringComparison.Ordinal)
                    && fileSystem.GetLastWriteTimeUtc(file) < cutoff)
                {
                    fileSystem.DeleteFile(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleaning up is best effort; the write itself goes on.
        }
    }
}
