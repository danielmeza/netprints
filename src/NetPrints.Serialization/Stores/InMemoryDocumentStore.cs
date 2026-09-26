#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Serialization.Stores;

/// <summary>
/// In-memory <see cref="IDocumentStore"/>, mainly for tests: documents are held as byte arrays keyed by
/// <see cref="DocumentId"/>, and <see cref="Set"/>/<see cref="Get"/> read and write them directly,
/// without a document format (document-format.md §2.7).
/// </summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<DocumentId, byte[]> documents = new();
    private readonly ConcurrentDictionary<DocumentId, SemaphoreSlim> locks = new();
    private readonly Subject<DocumentChange> changes = new();
    private readonly object disposeLock = new();
    private bool disposed;

    /// <inheritdoc/>
    public string DisplayName => "In-memory store";

    /// <inheritdoc/>
    public IObservable<DocumentChange> Changes => changes.AsObservable();

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(DocumentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(documents.ContainsKey(id));
    }

    /// <inheritdoc/>
    public ValueTask<Stream> OpenReadAsync(DocumentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!documents.TryGetValue(id, out byte[]? content))
        {
            throw new DocumentNotFoundException(id);
        }

        return ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(DocumentId id, Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        SemaphoreSlim gate = locks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var buffer = new MemoryStream();
            await write(buffer, cancellationToken).ConfigureAwait(false);
            documents[id] = buffer.ToArray();
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

        foreach (DocumentId id in documents.Keys
            .Where(i => i.Path.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(i => i.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return id;
        }
    }

    /// <summary>
    /// Sets <paramref name="id"/>'s content directly, raising <see cref="Changes"/> synchronously
    /// (<see cref="DocumentChangeKind.Created"/> if it did not already exist, otherwise
    /// <see cref="DocumentChangeKind.Changed"/>).
    /// </summary>
    /// <param name="id">Document id to set.</param>
    /// <param name="content">New content.</param>
    public void Set(DocumentId id, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        bool existed = documents.ContainsKey(id);
        documents[id] = content;

        // Under the same lock Dispose takes: Set can race with Dispose from another thread (both are
        // allowed concurrently), and Subject.OnNext after OnCompleted/Dispose throws.
        lock (disposeLock)
        {
            if (disposed)
            {
                return;
            }

            changes.OnNext(new DocumentChange(id, existed ? DocumentChangeKind.Changed : DocumentChangeKind.Created));
        }
    }

    /// <summary>
    /// Returns <paramref name="id"/>'s content.
    /// </summary>
    /// <param name="id">Document id to get.</param>
    /// <returns>The document's content.</returns>
    /// <exception cref="DocumentNotFoundException"><paramref name="id"/> does not exist.</exception>
    public byte[] Get(DocumentId id)
    {
        if (!documents.TryGetValue(id, out byte[]? content))
        {
            throw new DocumentNotFoundException(id);
        }

        return content;
    }

    /// <summary>Completes <see cref="Changes"/> and disposes the per-id write locks.</summary>
    public void Dispose()
    {
        lock (disposeLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            changes.OnCompleted();
            changes.Dispose();
        }

        foreach (SemaphoreSlim gate in locks.Values)
        {
            gate.Dispose();
        }
    }
}
