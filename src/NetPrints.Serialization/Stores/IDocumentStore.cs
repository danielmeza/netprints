#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Serialization.Stores;

/// <summary>Kind of change reported by <see cref="IDocumentStore.Changes"/>.</summary>
public enum DocumentChangeKind
{
    /// <summary>A document was created.</summary>
    Created,

    /// <summary>A document's content changed.</summary>
    Changed,

    /// <summary>A document was deleted.</summary>
    Deleted,
}

/// <summary>A change to a document of an <see cref="IDocumentStore"/> (document-format.md §2.7).</summary>
/// <param name="Id">Id of the changed document.</param>
/// <param name="Kind">Kind of change.</param>
public sealed record DocumentChange(DocumentId Id, DocumentChangeKind Kind);

/// <summary>
/// Reads, writes, lists and watches documents identified by a <see cref="DocumentId"/>, backed by a
/// file system directory (<see cref="FileSystemDocumentStore"/>) or in-memory storage
/// (<see cref="InMemoryDocumentStore"/>) (document-format.md §2.7). Implementations are safe to call
/// concurrently; writes to the same id are serialized.
/// </summary>
public interface IDocumentStore : IDisposable
{
    /// <summary>Human-readable description of this store (for diagnostics).</summary>
    string DisplayName { get; }

    /// <summary>Returns whether <paramref name="id"/> exists in this store.</summary>
    /// <param name="id">Document id to check.</param>
    /// <param name="cancellationToken">Token to cancel the check.</param>
    ValueTask<bool> ExistsAsync(DocumentId id, CancellationToken cancellationToken);

    /// <summary>
    /// Opens <paramref name="id"/> for reading. The caller owns and disposes the returned stream.
    /// </summary>
    /// <param name="id">Document id to open.</param>
    /// <param name="cancellationToken">Token to cancel the open.</param>
    /// <returns>A readable stream positioned at the start of the document.</returns>
    /// <exception cref="DocumentNotFoundException"><paramref name="id"/> does not exist.</exception>
    ValueTask<Stream> OpenReadAsync(DocumentId id, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically writes <paramref name="id"/>: <paramref name="write"/> fills a temporary location, and
    /// only on its successful completion does the temporary content replace the document in one step.
    /// On an exception (including cancellation) from <paramref name="write"/>, the temporary content is
    /// discarded, the existing document (if any) is untouched, and the exception propagates. Creates any
    /// missing containing directory.
    /// </summary>
    /// <param name="id">Document id to write.</param>
    /// <param name="write">Callback that writes the document's new content to the stream it is given.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    ValueTask WriteAsync(DocumentId id, Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken);

    /// <summary>
    /// Lists every document id starting with <paramref name="prefix"/>, ordinal-sorted, recursively.
    /// </summary>
    /// <param name="prefix">Prefix to match, or <c>""</c> for every document.</param>
    /// <param name="cancellationToken">Token to cancel the listing.</param>
    /// <returns>Matching document ids, ordinal-sorted.</returns>
    IAsyncEnumerable<DocumentId> ListAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>
    /// Changes to this store's documents caused by something other than this store's own
    /// <see cref="WriteAsync"/> (an external edit, or another process). Never calls
    /// <c>IObserver{T}.OnError</c>; completes when this store is disposed.
    /// </summary>
    IObservable<DocumentChange> Changes { get; }
}
