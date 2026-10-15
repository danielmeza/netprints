using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.State;

/// <summary>Moves <c>state/</c> and <c>backups/</c> from the former root to the current one, once (ADR-0033, FR-106).</summary>
public sealed class StateMigration
{
    private readonly EditorDataPaths paths;
    private readonly IEditorFileSystem fileSystem;
    private readonly IStateFolderMover mover;
    private readonly ILogger logger;

    /// <summary>Creates the migration.</summary>
    /// <param name="paths">The resolved paths; nothing is migrated when <see cref="EditorDataPaths.LegacyRoot"/> is null.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="mover">Moves a folder tree.</param>
    /// <param name="logger">Receives the failures.</param>
    public StateMigration(EditorDataPaths paths, IEditorFileSystem fileSystem, IStateFolderMover mover, ILogger<StateMigration> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(mover);
        ArgumentNullException.ThrowIfNull(logger);
        this.paths = paths;
        this.fileSystem = fileSystem;
        this.mover = mover;
        this.logger = logger;
    }

    /// <summary>Runs the migration. A folder already present at the new root wins and the old one is left alone; a failed move is logged and never thrown (FR-051).</summary>
    public void Run()
    {
        string? legacyRoot = paths.LegacyRoot;
        if (legacyRoot is null || string.Equals(Path.GetFullPath(legacyRoot), Path.GetFullPath(paths.Root), EditorDataPaths.PathComparison))
        {
            return;
        }

        bool moved = false;
        foreach (string destination in new[] { paths.StateDirectory, paths.BackupsDirectory })
        {
            moved |= TryMove(Path.Combine(legacyRoot, Path.GetFileName(destination)), destination);
        }

        if (moved)
        {
            RemoveIfEmpty(legacyRoot);
        }
    }

    private bool TryMove(string source, string destination)
    {
        if (!fileSystem.DirectoryExists(source) || fileSystem.DirectoryExists(destination))
        {
            return false;
        }

        try
        {
            fileSystem.CreateDirectory(paths.Root);
            mover.Move(source, destination);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.StateMigrationFailed(logger, exception, source, destination);
            return false;
        }
    }

    private void RemoveIfEmpty(string folder)
    {
        try
        {
            if (!fileSystem.EnumerateDirectories(folder).Any() && !fileSystem.EnumerateFiles(folder).Any())
            {
                fileSystem.DeleteDirectory(folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LegacyRootNotRemoved(logger, exception, folder);
        }
    }
}
