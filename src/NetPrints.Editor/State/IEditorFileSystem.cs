namespace NetPrints.Editor.State;

/// <summary>The file system operations behind the editor's per-user files (state, backups); in-memory in tests.</summary>
public interface IEditorFileSystem
{
    /// <summary>Tells whether a file exists.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool FileExists(string path);

    /// <summary>Tells whether a folder exists.</summary>
    /// <param name="path">The folder path.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool DirectoryExists(string path);

    /// <summary>Creates a folder and its missing parents, private to the user (<c>0700</c>) on Linux and macOS.</summary>
    /// <param name="path">The folder path.</param>
    void CreateDirectory(string path);

    /// <summary>Creates or replaces a file, private to the user (<c>0600</c>) on Linux and macOS.</summary>
    /// <param name="path">The file path; its folder exists.</param>
    /// <param name="bytes">The content.</param>
    void WriteAllBytes(string path, byte[] bytes);

    /// <summary>Reads a whole file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The content.</returns>
    byte[] ReadAllBytes(string path);

    /// <summary>Moves a file over a destination that may exist.</summary>
    /// <param name="source">The file to move.</param>
    /// <param name="destination">The path it replaces.</param>
    void Move(string source, string destination);

    /// <summary>Deletes a file; nothing happens when it does not exist.</summary>
    /// <param name="path">The file path.</param>
    void DeleteFile(string path);

    /// <summary>Deletes a folder and everything in it; nothing happens when it does not exist.</summary>
    /// <param name="path">The folder path.</param>
    void DeleteDirectory(string path);

    /// <summary>Lists the folders directly inside a folder.</summary>
    /// <param name="path">The folder path.</param>
    /// <returns>Their full paths; empty when the folder does not exist.</returns>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>Lists the files directly inside a folder.</summary>
    /// <param name="path">The folder path.</param>
    /// <returns>Their full paths; empty when the folder does not exist.</returns>
    IEnumerable<string> EnumerateFiles(string path);

    /// <summary>Gets the time a file was last written.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The time, in UTC.</returns>
    DateTime GetLastWriteTimeUtc(string path);
}
