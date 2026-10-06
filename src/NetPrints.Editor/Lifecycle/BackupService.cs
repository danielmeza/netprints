using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Lifecycle;

/// <summary>
/// Backs up the unsaved files of the open project outside the project folder (FR-024, FR-026, state-files.md §3). A file's
/// backup is written <see cref="DefaultDelay"/> after its last change; every change restarts the wait. A write failure never
/// interrupts editing: it is logged, and the first one of the session is also reported through the warning callback.
/// </summary>
public sealed class BackupService : IDisposable
{
    /// <summary>How long after a file's last change its backup is written.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(30);

    /// <summary>How long a backup is kept before the startup clean-up deletes it.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    /// <summary>The environment variable (milliseconds, test-only) that replaces <see cref="DefaultDelay"/>.</summary>
    public const string DelayVariable = "NETPRINTS_BACKUP_DELAY";

    /// <summary>The warning shown when a backup could not be written.</summary>
    public const string FailureMessage = "Backups are failing; see the log";

    private readonly BackupStore store;
    private readonly IEditorFileSystem fileSystem;
    private readonly TimeProvider time;
    private readonly TimeSpan delay;
    private readonly ILogger logger;
    private readonly Action<string> warn;
    private readonly Lock gate = new();
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> versions = new(StringComparer.Ordinal);
    private bool warned;
    private bool disposed;

    /// <summary>Creates the service of a project.</summary>
    /// <param name="paths">The editor's data folders.</param>
    /// <param name="fileSystem">The file system to write to.</param>
    /// <param name="time">The clock of the waits and of the recorded times.</param>
    /// <param name="projectPath">The project file's path.</param>
    /// <param name="delay">How long to wait after a change; <see cref="DefaultDelay"/> in production.</param>
    /// <param name="logger">Receives write failures.</param>
    /// <param name="warn">Shows the first failure of the session to the user.</param>
    public BackupService(EditorDataPaths paths, IEditorFileSystem fileSystem, TimeProvider time, string projectPath, TimeSpan delay, ILogger<BackupService> logger, Action<string> warn)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        this.warn = warn ?? throw new ArgumentNullException(nameof(warn));
        this.delay = delay;
        this.logger = logger;
        store = new BackupStore(paths, fileSystem, projectPath, logger);
    }

    /// <summary>Reads the wait from <see cref="DelayVariable"/>.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, or returns null when it is not set.</param>
    /// <returns>The variable's milliseconds when it is a positive whole number, else <see cref="DefaultDelay"/>.</returns>
    public static TimeSpan ResolveDelay(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        return int.TryParse(getEnvironmentVariable(DelayVariable), out int milliseconds) && milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : DefaultDelay;
    }

    /// <summary>Lists the project's backups.</summary>
    /// <returns>The entries.</returns>
    public IReadOnlyList<BackupEntry> List()
    {
        lock (gate)
        {
            return store.List();
        }
    }

    /// <summary>Reads the backed-up content of an entry.</summary>
    /// <param name="entry">An entry of <see cref="List"/>.</param>
    /// <returns>The content.</returns>
    public byte[] Read(BackupEntry entry)
    {
        lock (gate)
        {
            return store.Read(entry);
        }
    }

    /// <summary>Notes a change to a file: its backup is written <c>delay</c> from now, replacing a wait already running.</summary>
    /// <param name="originalPath">The file's path relative to the project, with <c>/</c> separators.</param>
    /// <param name="render">Renders the content to keep when the wait ends; returns null when there is nothing to keep any more.</param>
    public void Schedule(string originalPath, Func<Task<byte[]?>> render)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalPath);
        ArgumentNullException.ThrowIfNull(render);

        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            Cancel(originalPath);
            int version = versions[originalPath];
            pending[originalPath] = new Pending(render, time, delay, () => WriteAsync(originalPath, version, render).Forget(logger));
        }
    }

    /// <summary>Writes the backups still waiting now.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public async Task FlushAsync()
    {
        List<(string Path, int Version, Func<Task<byte[]?>> Render)> waiting = [];
        lock (gate)
        {
            foreach ((string path, Pending item) in pending.ToList())
            {
                Cancel(path);
                waiting.Add((path, versions[path], item.Render));
            }
        }

        foreach ((string path, int version, Func<Task<byte[]?>> render) in waiting)
        {
            await WriteAsync(path, version, render).ConfigureAwait(true);
        }
    }

    /// <summary>Deletes the backup of a file and cancels its wait; the project's folder goes with the last backup.</summary>
    /// <param name="originalPath">The file's path relative to the project.</param>
    public void Delete(string originalPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalPath);
        lock (gate)
        {
            Cancel(originalPath);
            Guarded(() => store.Delete(originalPath));
        }
    }

    /// <summary>Deletes every backup of the project and cancels every wait.</summary>
    public void DeleteAll()
    {
        lock (gate)
        {
            foreach (string path in pending.Keys.ToList())
            {
                Cancel(path);
            }

            Guarded(store.DeleteAll);
        }
    }

    /// <summary>Cancels the waits; backups already written stay, as the safety net of a crash or a quit without saving.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            foreach (string path in pending.Keys.ToList())
            {
                Cancel(path);
            }
        }
    }

    /// <summary>
    /// Startup clean-up: deletes backups older than <see cref="MaxAge"/> and backup folders whose project file is gone or that
    /// have none left. A folder whose manifest cannot be read is left alone. A failure is logged, never thrown.
    /// </summary>
    /// <param name="paths">The editor's data folders.</param>
    /// <param name="fileSystem">The file system to clean.</param>
    /// <param name="time">The clock that tells how old a backup is.</param>
    /// <param name="logger">Receives failures.</param>
    public static void CleanUp(EditorDataPaths paths, IEditorFileSystem fileSystem, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        DateTime oldest = time.GetUtcNow().UtcDateTime - MaxAge;
        foreach (string folder in fileSystem.EnumerateDirectories(paths.BackupsDirectory).ToList())
        {
            try
            {
                CleanUpFolder(folder, fileSystem, oldest, logger);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.BackupCleanUpFailed(logger, ex, folder);
            }
        }
    }

    private static void CleanUpFolder(string folder, IEditorFileSystem fileSystem, DateTime oldest, ILogger logger)
    {
        BackupManifest? manifest = BackupStore.ReadManifest(fileSystem, Path.Combine(folder, "manifest.json"), logger);
        if (manifest is null)
        {
            return;
        }

        var store = new BackupStore(folder, manifest.ProjectPath, fileSystem, logger);
        if (!fileSystem.FileExists(manifest.ProjectPath))
        {
            store.DeleteAll();
            return;
        }

        foreach (BackupEntry entry in manifest.Files.Where(entry => entry.WrittenUtc < oldest).ToList())
        {
            store.DeleteFile(entry);
            manifest.Files.Remove(entry);
        }

        store.Save(manifest);
    }

    private void Cancel(string path)
    {
        if (pending.Remove(path, out Pending? waiting))
        {
            waiting.Dispose();
        }

        versions[path] = versions.GetValueOrDefault(path) + 1;
    }

    private async Task WriteAsync(string path, int version, Func<Task<byte[]?>> render)
    {
        try
        {
            byte[]? content = await render().ConfigureAwait(true);
            lock (gate)
            {
                if (versions.GetValueOrDefault(path) != version)
                {
                    return;
                }

                if (pending.Remove(path, out Pending? finished))
                {
                    finished.Dispose();
                }

                if (content is not null && !disposed)
                {
                    store.Write(path, content, time.GetUtcNow().UtcDateTime);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fail(ex);
        }
    }

    private void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(ex);
        }
    }

    private void Fail(Exception ex)
    {
        Log.BackupFailed(logger, ex);
        bool first;
        lock (gate)
        {
            first = !warned;
            warned = true;
        }

        if (first)
        {
            warn(FailureMessage);
        }
    }

    private sealed class Pending(Func<Task<byte[]?>> render, TimeProvider time, TimeSpan delay, Action fire) : IDisposable
    {
        private readonly ITimer timer = time.CreateTimer(_ => fire(), null, delay, Timeout.InfiniteTimeSpan);

        public Func<Task<byte[]?>> Render { get; } = render;

        public void Dispose() => timer.Dispose();
    }
}
