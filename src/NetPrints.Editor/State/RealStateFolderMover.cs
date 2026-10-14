using System.Runtime.Versioning;

namespace NetPrints.Editor.State;

/// <summary>The <see cref="IStateFolderMover"/> over the real file system.</summary>
public sealed class RealStateFolderMover : IStateFolderMover
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly Action<string, string> rename;

    /// <summary>Creates a mover that renames the folder and falls back to copy-verify-delete when it cannot.</summary>
    public RealStateFolderMover()
        : this(Directory.Move)
    {
    }

    /// <summary>Creates a mover with its own rename step, for tests.</summary>
    /// <param name="rename">Renames a folder; an <see cref="IOException"/> sends the mover to the copy fallback.</param>
    public RealStateFolderMover(Action<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        this.rename = rename;
    }

    /// <summary>Moves the folder, then makes the result readable by the user only on Linux and macOS (FR-052).</summary>
    /// <param name="source">The folder to move.</param>
    /// <param name="destination">The new path; its parent exists.</param>
    /// <exception cref="IOException">The folder could not be moved or the copy did not verify; the source is left as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be moved; the source is left as it was.</exception>
    public void Move(string source, string destination)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        if (!TryRename(source, destination))
        {
            CopyVerifyDelete(source, destination);
        }

        if (!OperatingSystem.IsWindows())
        {
            MakePrivate(destination);
        }
    }

    private static void CopyVerifyDelete(string source, string destination)
    {
        bool copied = false;
        try
        {
            Copy(new DirectoryInfo(source), destination);
            if (Measure(source) != Measure(destination))
            {
                throw new IOException("The copy of the state folder does not match the original.");
            }

            copied = true;
        }
        finally
        {
            if (!copied && Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }

        Directory.Delete(source, recursive: true);
    }

    private static void Copy(DirectoryInfo source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (FileInfo file in source.EnumerateFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name));
        }

        foreach (DirectoryInfo directory in source.EnumerateDirectories())
        {
            Copy(directory, Path.Combine(destination, directory.Name));
        }
    }

    private static (int Files, long Bytes) Measure(string folder)
    {
        FileInfo[] files = new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories);
        return (files.Length, files.Sum(file => file.Length));
    }

    [UnsupportedOSPlatform("windows")]
    private static void MakePrivate(string folder)
    {
        File.SetUnixFileMode(folder, PrivateDirectory);
        foreach (FileSystemInfo entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (entry.LinkTarget is null)
            {
                File.SetUnixFileMode(entry.FullName, entry is DirectoryInfo ? PrivateDirectory : PrivateFile);
            }
        }
    }

    private bool TryRename(string source, string destination)
    {
        try
        {
            rename(source, destination);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
