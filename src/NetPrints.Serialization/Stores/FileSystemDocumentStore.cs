#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetPrints.Serialization.Stores;

/// <summary>
/// File system <see cref="IDocumentStore"/>: documents are files under <see cref="RootDirectory"/>, a
/// document id is its path relative to that directory, and <see cref="IDocumentStore.Changes"/> is
/// backed by a recursive <see cref="FileSystemWatcher"/> (document-format.md §2.7).
/// </summary>
public sealed class FileSystemDocumentStore : IDocumentStore
{
    /// <summary>How long <see cref="Changes"/> waits, per id, for further raw file system events before
    /// emitting one (also how long a change caused by this store's own <see cref="WriteAsync"/> is
    /// suppressed for).</summary>
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(200);

    private readonly IScheduler scheduler;
    private readonly ILogger<FileSystemDocumentStore> logger;
    private readonly FileSystemWatcher watcher;
    private readonly Subject<DocumentChange> changes = new();
    private readonly ConcurrentDictionary<DocumentId, SemaphoreSlim> locks = new();
    private readonly ConcurrentDictionary<DocumentId, IDisposable> pendingTimers = new();
    private readonly ConcurrentDictionary<DocumentId, DateTimeOffset> ownWriteAt = new();
    private readonly object disposeLock = new();
    private bool disposed;

    /// <summary>
    /// Creates a file system document store rooted at <paramref name="rootDirectory"/>, creating it if
    /// it does not exist yet, and starts watching it for external changes.
    /// </summary>
    /// <param name="rootDirectory">Directory documents are stored under.</param>
    /// <param name="scheduler">Scheduler <see cref="Changes"/> debounces and emits on.</param>
    /// <param name="logger">Logger for external changes (event 3005).</param>
    public FileSystemDocumentStore(string rootDirectory, IScheduler scheduler, ILogger<FileSystemDocumentStore> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);

        watcher = new FileSystemWatcher(RootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        watcher.Changed += HandleChanged;
        watcher.Created += HandleChanged;
        watcher.Deleted += HandleDeleted;
        watcher.Renamed += HandleRenamed;
        watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc/>
    public string DisplayName => $"File system: {RootDirectory}";

    /// <summary>Directory documents are stored under (an absolute path).</summary>
    public string RootDirectory { get; }

    /// <inheritdoc/>
    public IObservable<DocumentChange> Changes => changes.AsObservable();

    /// <summary>Returns the absolute file path <paramref name="id"/> is (or would be) stored at.</summary>
    /// <param name="id">Document id to resolve.</param>
    /// <returns><paramref name="id"/>'s absolute file path under <see cref="RootDirectory"/>.</returns>
    public string GetFullPath(DocumentId id) =>
        Path.Combine(RootDirectory, id.Path.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Returns the document id of <paramref name="fullPath"/>, a file under <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">Directory the store is rooted at.</param>
    /// <param name="fullPath">Absolute (or root-relative) path of a file under <paramref name="rootDirectory"/>.</param>
    /// <returns><paramref name="fullPath"/>'s document id.</returns>
    /// <exception cref="ArgumentException"><paramref name="fullPath"/> is not under <paramref name="rootDirectory"/>.</exception>
    public static DocumentId ToDocumentId(string rootDirectory, string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        ArgumentException.ThrowIfNullOrEmpty(fullPath);

        string fullRoot = Path.GetFullPath(rootDirectory);
        string fullChild = Path.GetFullPath(fullPath, fullRoot);
        string relative = Path.GetRelativePath(fullRoot, fullChild);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new ArgumentException($"'{fullPath}' is not inside '{rootDirectory}'.", nameof(fullPath));
        }

        return new DocumentId(relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/'));
    }

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(DocumentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(File.Exists(GetFullPath(id)));
    }

    /// <inheritdoc/>
    public ValueTask<Stream> OpenReadAsync(DocumentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = GetFullPath(id);
        if (!File.Exists(path))
        {
            throw new DocumentNotFoundException(id);
        }

        return ValueTask.FromResult<Stream>(File.OpenRead(path));
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(DocumentId id, Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        SemaphoreSlim gate = locks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string targetPath = GetFullPath(id);
            string directory = Path.GetDirectoryName(targetPath) ?? RootDirectory;
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, $"{Path.GetFileName(targetPath)}.tmp-{Guid.NewGuid():N}");
            try
            {
                await using (FileStream tempStream = File.Open(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await write(tempStream, cancellationToken).ConfigureAwait(false);
                }

                // Own writes are suppressed (not reported through Changes): record the time before the
                // move so the resulting raw file system event (a Renamed event: the temp path to the
                // target path) is recognized and dropped as soon as it arrives.
                ownWriteAt[id] = scheduler.Now;
                File.Move(tempPath, targetPath, overwrite: true);
            }
            catch
            {
                ownWriteAt.TryRemove(id, out _);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DocumentId> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        await Task.Yield();

        if (!Directory.Exists(RootDirectory))
        {
            yield break;
        }

        var ids = new List<DocumentId>();
        foreach (string path in Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsTempFile(path))
            {
                continue;
            }

            DocumentId id = ToDocumentId(RootDirectory, path);
            if (id.Path.StartsWith(prefix, StringComparison.Ordinal))
            {
                ids.Add(id);
            }
        }

        foreach (DocumentId id in ids.OrderBy(i => i.Path, StringComparer.Ordinal))
        {
            yield return id;
        }
    }

    private static bool IsTempFile(string path) => Path.GetFileName(path).Contains(".tmp-", StringComparison.Ordinal);

    private void HandleChanged(object sender, FileSystemEventArgs e) =>
        TryHandleRawChange(e.FullPath, e.ChangeType == WatcherChangeTypes.Created ? DocumentChangeKind.Created : DocumentChangeKind.Changed);

    private void HandleDeleted(object sender, FileSystemEventArgs e) => TryHandleRawChange(e.FullPath, DocumentChangeKind.Deleted);

    private void HandleRenamed(object sender, RenamedEventArgs e)
    {
        TryHandleRawChange(e.OldFullPath, DocumentChangeKind.Deleted);
        TryHandleRawChange(e.FullPath, DocumentChangeKind.Created);
    }

    private void TryHandleRawChange(string fullPath, DocumentChangeKind kind)
    {
        if (IsTempFile(fullPath))
        {
            return;
        }

        DocumentId id;
        try
        {
            id = ToDocumentId(RootDirectory, fullPath);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (ownWriteAt.TryGetValue(id, out DateTimeOffset writtenAt) && scheduler.Now - writtenAt <= ThrottleWindow)
        {
            // Caused by this store's own WriteAsync; consume the marker and drop the raw event.
            ownWriteAt.TryRemove(id, out _);
            return;
        }

        if (pendingTimers.TryRemove(id, out IDisposable? existing))
        {
            existing.Dispose();
        }

        pendingTimers[id] = scheduler.Schedule(ThrottleWindow, () =>
        {
            pendingTimers.TryRemove(id, out _);

            // A raw event can still be in flight (queued on a background thread before
            // EnableRaisingEvents was cleared) when Dispose runs; without this check-and-emit under
            // the same lock Dispose takes, this could call OnNext after changes has been completed
            // and disposed.
            lock (disposeLock)
            {
                if (disposed)
                {
                    return;
                }

                Log.ExternalChange(logger, kind, id);
                changes.OnNext(new DocumentChange(id, kind));
            }
        });
    }

    /// <summary>Stops the file system watcher, completes <see cref="Changes"/>, and disposes the per-id
    /// write locks and any still-pending debounce timers.</summary>
    public void Dispose()
    {
        lock (disposeLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleChanged;
            watcher.Created -= HandleChanged;
            watcher.Deleted -= HandleDeleted;
            watcher.Renamed -= HandleRenamed;
            watcher.Dispose();

            foreach (IDisposable timer in pendingTimers.Values)
            {
                timer.Dispose();
            }

            changes.OnCompleted();
            changes.Dispose();
        }

        foreach (SemaphoreSlim gate in locks.Values)
        {
            gate.Dispose();
        }
    }
}
