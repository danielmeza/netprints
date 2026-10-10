using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Lifecycle;

/// <summary>
/// The backup files of one project under <c>&lt;root&gt;/backups/&lt;project-key&gt;</c> (state-files.md §1–§3): the backed-up
/// content of each file and the <c>manifest.json</c> that lists them. Recovery reads the same files through <see cref="List"/> and
/// <see cref="Read"/>. Every write is atomic, and nothing is ever written inside the project folder. Not thread safe.
/// </summary>
public sealed class BackupStore
{
    /// <summary>The manifest's schema version.</summary>
    public const int SchemaVersion = 1;

    private const string ManifestName = "manifest.json";
    private const string BackupSuffix = ".bak.json";
    private const string RootedFolder = "__rooted";
    private const int RootedHashLength = 16;

    private readonly IEditorFileSystem fileSystem;
    private readonly AtomicFileWriter writer;
    private readonly ILogger logger;

    /// <summary>Creates the store of a project.</summary>
    /// <param name="paths">The editor's data folders.</param>
    /// <param name="fileSystem">The file system to use.</param>
    /// <param name="projectPath">The project file's path.</param>
    /// <param name="logger">Receives warnings about unreadable manifests.</param>
    public BackupStore(EditorDataPaths paths, IEditorFileSystem fileSystem, string projectPath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        writer = new AtomicFileWriter(fileSystem);
        ProjectPath = projectPath;
        Folder = paths.BackupDirectoryOf(EditorDataPaths.ProjectKey(projectPath));
    }

    /// <summary>Creates the store of an existing backup folder.</summary>
    /// <param name="folder">The backup folder.</param>
    /// <param name="projectPath">The project file's path.</param>
    /// <param name="fileSystem">The file system to use.</param>
    /// <param name="logger">Receives warnings about unreadable manifests.</param>
    internal BackupStore(string folder, string projectPath, IEditorFileSystem fileSystem, ILogger logger)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
        writer = new AtomicFileWriter(fileSystem);
        ProjectPath = projectPath;
        Folder = folder;
    }

    /// <summary>Gets the project file's path.</summary>
    public string ProjectPath { get; }

    /// <summary>Gets the project's backup folder.</summary>
    public string Folder { get; }

    private string ManifestPath => Path.Combine(Folder, ManifestName);

    /// <summary>Lists the project's backups.</summary>
    /// <returns>The entries; empty when there are none or the manifest is unreadable.</returns>
    public IReadOnlyList<BackupEntry> List() => ReadManifest(fileSystem, ManifestPath, logger)?.Files ?? [];

    /// <summary>Reads the backed-up content of an entry.</summary>
    /// <param name="entry">An entry of <see cref="List"/>.</param>
    /// <returns>The content.</returns>
    /// <exception cref="InvalidDataException">The entry's backup path leaves the backup folder.</exception>
    public byte[] Read(BackupEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return fileSystem.ReadAllBytes(Resolve(entry.BackupPath));
    }

    /// <summary>Writes the backup of a file and lists it in the manifest, replacing an earlier backup of the same file.</summary>
    /// <param name="originalPath">The file's path relative to the project, with <c>/</c> separators.</param>
    /// <param name="content">The content to keep.</param>
    /// <param name="writtenUtc">The time to record.</param>
    public void Write(string originalPath, byte[] content, DateTime writtenUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalPath);
        ArgumentNullException.ThrowIfNull(content);

        BackupManifest manifest = ReadOwnManifest() ?? new BackupManifest(SchemaVersion, ProjectPath, []);
        string backupPath = BackupPathOf(originalPath);
        writer.Write(Resolve(backupPath), content);

        manifest.Files.RemoveAll(entry => entry.OriginalPath == originalPath);
        manifest.Files.Add(new BackupEntry(originalPath, backupPath, TruncateToSeconds(writtenUtc), Convert.ToHexStringLower(SHA256.HashData(content))));
        WriteManifest(manifest);
    }

    /// <summary>Deletes the backup of a file, and the project's folder when none is left.</summary>
    /// <param name="originalPath">The file's path relative to the project.</param>
    /// <exception cref="IOException">The folder holds a manifest this version cannot use, which is left alone.</exception>
    public void Delete(string originalPath)
    {
        BackupManifest? manifest = ReadOwnManifest();
        if (manifest is null)
        {
            return;
        }

        foreach (BackupEntry entry in manifest.Files.Where(entry => entry.OriginalPath == originalPath).ToList())
        {
            DeleteFile(entry);
            manifest.Files.Remove(entry);
        }

        Save(manifest);
    }

    /// <summary>Deletes every backup of the project, with its folder.</summary>
    /// <exception cref="IOException">The folder holds a manifest this version cannot use, which is left alone.</exception>
    public void DeleteAll()
    {
        _ = ReadOwnManifest();
        fileSystem.DeleteDirectory(Folder);
    }

    /// <summary>Applies a change to the manifest of a folder and writes it back, or deletes the folder when no entry is left.</summary>
    /// <param name="manifest">The manifest after the change.</param>
    internal void Save(BackupManifest manifest)
    {
        if (manifest.Files.Count == 0)
        {
            fileSystem.DeleteDirectory(Folder);
        }
        else
        {
            WriteManifest(manifest);
        }
    }

    /// <summary>Deletes the backup file of an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <exception cref="InvalidDataException">The entry's backup path leaves the backup folder: nothing is deleted.</exception>
    internal void DeleteFile(BackupEntry entry) => fileSystem.DeleteFile(Resolve(entry.BackupPath));

    /// <summary>Reads a manifest.</summary>
    /// <param name="fileSystem">The file system to read from.</param>
    /// <param name="manifestPath">The manifest's path.</param>
    /// <param name="logger">Receives a warning when the manifest is unreadable or from a newer schema.</param>
    /// <returns>The manifest, or null when it is missing, unreadable or has another schema version.</returns>
    internal static BackupManifest? ReadManifest(IEditorFileSystem fileSystem, string manifestPath, ILogger logger) =>
        ReadManifest(fileSystem, manifestPath, logger, out _);

    private static BackupManifest? ReadManifest(IEditorFileSystem fileSystem, string manifestPath, ILogger logger, out bool foreign)
    {
        foreign = false;
        if (!fileSystem.FileExists(manifestPath))
        {
            return null;
        }

        try
        {
            BackupManifest? manifest = JsonSerializer.Deserialize(fileSystem.ReadAllBytes(manifestPath), BackupJsonContext.Default.BackupManifest);
            if (manifest is { SchemaVersion: SchemaVersion, Files: not null })
            {
                return manifest;
            }

            Log.BackupManifestUnsupported(logger, manifestPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.BackupManifestUnreadable(logger, ex, manifestPath);
        }

        foreign = true;
        return null;
    }

    // A manifest that exists but cannot be used belongs to another version of the editor: nothing in its folder is written or deleted.
    private BackupManifest? ReadOwnManifest()
    {
        BackupManifest? manifest = ReadManifest(fileSystem, ManifestPath, logger, out bool foreign);
        return foreign ? throw new IOException($"The backup manifest '{ManifestPath}' is not usable by this version, so its folder is left alone.") : manifest;
    }

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    // A path that climbs out of the project (a class in a sibling folder) keeps its place inside the backup folder, and a rooted
    // one (a class on another drive) goes under a folder named by its hash, so two roots never share a file.
    private static string BackupPathOf(string originalPath)
    {
        string[] segments = originalPath.Replace('\\', '/').Split('/');
        bool rooted = segments[0].Length == 0 || segments[0].Contains(':', StringComparison.Ordinal);
        return rooted
            ? $"{RootedFolder}/{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(originalPath)))[..RootedHashLength]}/{segments[^1]}{BackupSuffix}"
            : string.Join('/', segments.Select(segment => segment == ".." ? "__" : segment)) + BackupSuffix;
    }

    // The one way a backup path becomes a file path: anything that does not end up inside the folder is refused.
    private string Resolve(string relativePath)
    {
        string combined = Path.Combine(Folder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(combined).StartsWith(root, StringComparison.Ordinal)
            ? combined
            : throw new InvalidDataException($"The backup path '{relativePath}' is outside the backup folder.");
    }

    private void WriteManifest(BackupManifest manifest) =>
        writer.Write(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupJsonContext.Default.BackupManifest));
}
