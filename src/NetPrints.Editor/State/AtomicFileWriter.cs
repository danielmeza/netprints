namespace NetPrints.Editor.State;

/// <summary>
/// Writes the editor's per-user files atomically (state-files.md §1): the content goes to <c>&lt;file&gt;.tmp</c> in the same
/// folder and is then renamed over the target, so a crash leaves the old file or the new one, never half of one.
/// </summary>
/// <param name="fileSystem">The file system to write to.</param>
public sealed class AtomicFileWriter(IEditorFileSystem fileSystem)
{
    private const string TempSuffix = ".tmp";

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

        string temp = path + TempSuffix;
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
}
