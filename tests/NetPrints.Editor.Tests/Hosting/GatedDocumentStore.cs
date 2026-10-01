using NetPrints.Serialization;
using NetPrints.Serialization.Stores;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>An <see cref="IDocumentStore"/> whose writes block until <see cref="Release"/>, so a test can hold a save in progress.</summary>
public sealed class GatedDocumentStore(IDocumentStore inner) : IDocumentStore
{
    private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int writes;

    /// <summary>How many writes reached the store.</summary>
    public int WriteCalls => Volatile.Read(ref writes);

    /// <summary>Completes when the first write reached the gate.</summary>
    public Task FirstWriteStarted => firstWrite.Task;

    /// <summary>Lets every blocked and later write through.</summary>
    public void Release() => gate.TrySetResult();

    /// <inheritdoc/>
    public string DisplayName => inner.DisplayName;

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(DocumentId id, CancellationToken cancellationToken) => inner.ExistsAsync(id, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Stream> OpenReadAsync(DocumentId id, CancellationToken cancellationToken) => inner.OpenReadAsync(id, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask WriteAsync(DocumentId id, Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref writes);
        firstWrite.TrySetResult();
        await gate.Task.WaitAsync(cancellationToken);
        await inner.WriteAsync(id, write, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DocumentId> ListAsync(string prefix, CancellationToken cancellationToken) => inner.ListAsync(prefix, cancellationToken);

    /// <inheritdoc/>
    public IObservable<DocumentChange> Changes => inner.Changes;

    /// <inheritdoc/>
    public void Dispose() => inner.Dispose();
}
