using System.Runtime.Versioning;

namespace NetPrints.Editor.State;

/// <summary>The <see cref="IEditorFileSystem"/> over the real file system.</summary>
public sealed class RealEditorFileSystem : IEditorFileSystem
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            CreatePrivateDirectory(Path.GetFullPath(path));
        }
    }

    // Directory.CreateDirectory applies the mode to the last folder only, so each missing parent is created in turn.
    [UnsupportedOSPlatform("windows")]
    private static void CreatePrivateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        string? parent = Path.GetDirectoryName(path);
        if (parent is not null)
        {
            CreatePrivateDirectory(parent);
        }

        Directory.CreateDirectory(path, PrivateDirectory);
    }

    /// <inheritdoc/>
    public void WriteAllBytes(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFile;
        }

        using var stream = new FileStream(path, options);
        stream.Write(bytes);
    }

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <inheritdoc/>
    public void Move(string source, string destination) => File.Move(source, destination, overwrite: true);

    /// <inheritdoc/>
    public void DeleteFile(string path) => File.Delete(path);

    /// <inheritdoc/>
    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.EnumerateDirectories(path) : [];

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFiles(string path) =>
        Directory.Exists(path) ? Directory.EnumerateFiles(path) : [];

    /// <inheritdoc/>
    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
}
