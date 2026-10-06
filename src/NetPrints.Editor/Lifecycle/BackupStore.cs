using System.Security.Cryptography;
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
        string full = Path.GetFullPath(ToFilePath(entry.BackupPath));
        string root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal)
            ? fileSystem.ReadAllBytes(ToFilePath(entry.BackupPath))
            : throw new InvalidDataException($"The backup path '{entry.BackupPath}' is outside the backup folder.");
    }

    /// <summary>Writes the backup of a file and lists it in the manifest, replacing an earlier backup of the same file.</summary>
    /// <param name="originalPath">The file's path relative to the project, with <c>/</c> separators.</param>
    /// <param name="content">The content to keep.</param>
    /// <param name="writtenUtc">The time to record.</param>
    public void Write(string originalPath, byte[] content, DateTime writtenUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalPath);
        ArgumentNullException.ThrowIfNull(content);

        string backupPath = BackupPathOf(originalPath);
        writer.Write(ToFilePath(backupPath), content);

        BackupManifest manifest = ReadManifest(fileSystem, ManifestPath, logger) ?? new BackupManifest(SchemaVersion, ProjectPath, []);
        manifest.Files.RemoveAll(entry => entry.OriginalPath == originalPath);
        manifest.Files.Add(new BackupEntry(originalPath, backupPath, TruncateToSeconds(writtenUtc), Convert.ToHexStringLower(SHA256.HashData(content))));
        WriteManifest(manifest);
    }

    /// <summary>Deletes the backup of a file, and the project's folder when none is left.</summary>
    /// <param name="originalPath">The file's path relative to the project.</param>
    public void Delete(string originalPath)
    {
        BackupManifest? manifest = ReadManifest(fileSystem, ManifestPath, logger);
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
    public void DeleteAll() => fileSystem.DeleteDirectory(Folder);

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
    internal void DeleteFile(BackupEntry entry) => fileSystem.DeleteFile(ToFilePath(entry.BackupPath));

    /// <summary>Reads a manifest.</summary>
    /// <param name="fileSystem">The file system to read from.</param>
    /// <param name="manifestPath">The manifest's path.</param>
    /// <param name="logger">Receives a warning when the manifest is unreadable or from a newer schema.</param>
    /// <returns>The manifest, or null when it is missing, unreadable or has another schema version.</returns>
    internal static BackupManifest? ReadManifest(IEditorFileSystem fileSystem, string manifestPath, ILogger logger)
    {
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

        return null;
    }

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    // A path that climbs out of the project (a class in a sibling folder) keeps its place inside the backup folder.
    private static string BackupPathOf(string originalPath) =>
        string.Join('/', originalPath.Split('/').Select(segment => segment == ".." ? "__" : segment)) + BackupSuffix;

    private string ToFilePath(string relativePath) =>
        Path.Combine(Folder, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private void WriteManifest(BackupManifest manifest) =>
        writer.Write(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupJsonContext.Default.BackupManifest));
}
