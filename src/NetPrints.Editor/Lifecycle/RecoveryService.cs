using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Serialization;

namespace NetPrints.Editor.Lifecycle;

/// <summary>What recovery did to a project that was being opened.</summary>
/// <param name="Restored">The classes loaded from their backups, unsaved.</param>
/// <param name="Issues">The backups that could not be restored.</param>
public sealed record RecoveryResult(IReadOnlyList<ClassGraph> Restored, IReadOnlyList<DocumentIssue> Issues)
{
    /// <summary>Nothing was restored.</summary>
    public static RecoveryResult None { get; } = new([], []);
}

/// <summary>
/// Offers the backups of a project that is being opened (FR-025, state-files.md §3): the dialog lists each backed-up file with a note
/// when the file on disk is newer, Restore loads the backed-up content as unsaved changes and keeps the backup until the file is
/// saved or discarded, and Discard deletes the backups it offered. The answer chooses per file: Restore applies to the files it names and
/// deletes the backups of the other offered files. A backup of a class the project has no file for (never saved) is offered too
/// and restored as a new, unsaved class.
/// </summary>
public sealed class RecoveryService
{
    private readonly BackupStore store;
    private readonly ProjectPersistence persistence;
    private readonly ILogger logger;

    /// <summary>Creates the service of a project.</summary>
    /// <param name="store">The project's backups.</param>
    /// <param name="persistence">Reads the backed-up graph files.</param>
    /// <param name="logger">Receives the warnings about backups that cannot be restored.</param>
    public RecoveryService(BackupStore store, ProjectPersistence persistence, ILogger logger)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Offers the backups of a project that has just been loaded, and applies the answer.</summary>
    /// <param name="project">The loaded project, whose classes a restore replaces; it is not open in a session yet.</param>
    /// <param name="dialogs">Asks the user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The restored classes and the backups that could not be restored; <see cref="RecoveryResult.None"/> when there is nothing to offer, or the answer was not Restore.</returns>
    public async Task<RecoveryResult> RecoverAsync(Project project, IEditorDialogs dialogs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dialogs);

        List<(BackupEntry Entry, ClassGraph? Class)> offered = [];
        foreach (BackupEntry entry in store.List())
        {
            ClassGraph? cls = project.Classes.FirstOrDefault(
                candidate => string.Equals(ClassPaths.Of(project, candidate), entry.OriginalPath, StringComparison.Ordinal));
            if (cls is not null || IsClassPath(entry.OriginalPath))
            {
                offered.Add((entry, cls));
            }
        }

        if (offered.Count == 0)
        {
            return RecoveryResult.None;
        }

        RecoveryAnswer answer = await dialogs.ConfirmRecoverAsync(
            [.. offered.Select(item => new RecoveryFile(
                item.Entry.OriginalPath, item.Entry.WrittenUtc, item.Class is not null && IsOlderThanFile(project, item.Class, item.Entry), item.Class is null))])
            .ConfigureAwait(true);

        switch (answer.Choice)
        {
            case RecoveryChoice.Discard:
                offered.ForEach(item => store.Delete(item.Entry.OriginalPath));
                return RecoveryResult.None;
            case RecoveryChoice.Restore:
                HashSet<string> chosen = [.. answer.RestorePaths];
                offered.Where(item => !chosen.Contains(item.Entry.OriginalPath)).ToList().ForEach(item => store.Delete(item.Entry.OriginalPath));
                return await RestoreAsync(project, [.. offered.Where(item => chosen.Contains(item.Entry.OriginalPath))], cancellationToken).ConfigureAwait(true);
            default:
                return RecoveryResult.None;
        }
    }

    // A backup that matches no class of the project is a class that was never saved before the editor stopped (FR-025).
    private static bool IsClassPath(string path) => path.EndsWith(".netpc.json", StringComparison.Ordinal);

    private static bool IsOlderThanFile(Project project, ClassGraph cls, BackupEntry entry)
    {
        string file = cls.LoadedGraphFilePath ?? project.GetGraphFilePath(cls);
        return File.Exists(file) && File.GetLastWriteTimeUtc(file) > entry.WrittenUtc;
    }

    private async Task<RecoveryResult> RestoreAsync(Project project, List<(BackupEntry Entry, ClassGraph? Class)> offered, CancellationToken cancellationToken)
    {
        List<ClassGraph> restored = [];
        List<DocumentIssue> issues = [];
        foreach ((BackupEntry entry, ClassGraph? cls) in offered)
        {
            try
            {
                byte[] content = store.Read(entry);
                (ClassGraph restoredClass, IReadOnlyList<DocumentIssue> mapped) = cls is null
                    ? await persistence.RestoreNewClassAsync(project, entry.OriginalPath, content, cancellationToken).ConfigureAwait(true)
                    : await persistence.RestoreClassAsync(project, cls, content, cancellationToken).ConfigureAwait(true);
                restored.Add(restoredClass);
                issues.AddRange(mapped);
            }
            catch (Exception ex) when (ex is DocumentFormatException or InvalidDataException or IOException or ArgumentException or InvalidOperationException)
            {
                Log.RecoveryFailed(logger, ex, entry.OriginalPath);
                issues.Add(new DocumentIssue(DocumentIssueSeverity.Error, DocumentIssue.DocumentUnreadable,
                    $"The backup of {entry.OriginalPath} could not be restored and is kept: {ex.Message}", new Serialization.DocumentId(Path.GetFileName(entry.OriginalPath))));
            }
        }

        return new RecoveryResult(restored, issues);
    }
}
