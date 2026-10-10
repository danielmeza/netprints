using NetPrints.Editor.State;

namespace NetPrints.Editor.Lifecycle;

/// <summary>What a host needs for the open project to be backed up (FR-024).</summary>
/// <param name="Paths">The editor's data folders.</param>
/// <param name="FileSystem">The file system the backups are written to.</param>
/// <param name="Time">The clock of the backup waits.</param>
/// <param name="Delay">How long after a change a file's backup is written.</param>
public sealed record BackupOptions(EditorDataPaths Paths, IEditorFileSystem FileSystem, TimeProvider Time, TimeSpan Delay);
