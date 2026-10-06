namespace NetPrints.Editor.Lifecycle;

/// <summary>One backed-up file in a project's <c>manifest.json</c> (state-files.md §2).</summary>
/// <param name="OriginalPath">The file's path relative to the project, with <c>/</c> separators.</param>
/// <param name="BackupPath">The backup file's path relative to the project's backup folder, with <c>/</c> separators.</param>
/// <param name="WrittenUtc">When the backup was written, to the second.</param>
/// <param name="Sha256">The lowercase hex SHA-256 of the backup's content.</param>
public sealed record BackupEntry(string OriginalPath, string BackupPath, DateTime WrittenUtc, string Sha256);

/// <summary>A project's <c>manifest.json</c>.</summary>
/// <param name="SchemaVersion">The format version; <c>1</c>.</param>
/// <param name="ProjectPath">The project file the backups belong to.</param>
/// <param name="Files">The backed-up files.</param>
public sealed record BackupManifest(int SchemaVersion, string ProjectPath, List<BackupEntry> Files);
