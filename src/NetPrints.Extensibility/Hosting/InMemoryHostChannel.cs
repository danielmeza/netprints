using System.Reactive.Subjects;

namespace NetPrints.Extensibility.Hosting;

/// <summary>
/// One end of an in-process channel pair, for tests and embedding (extension-points.md §6). What one end sends the
/// other receives; disposing either end closes both.
/// </summary>
public sealed class InMemoryHostChannel : IHostChannel
{
    private readonly Subject<HostMessage> incoming = new();
    private readonly Lock gate = new();
    private InMemoryHostChannel? peer;
    private int closed;

    private InMemoryHostChannel(string id)
    {
        Id = id;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public HostChannelState State => Volatile.Read(ref closed) == 0 ? HostChannelState.Open : HostChannelState.Closed;

    /// <inheritdoc/>
    public IObservable<HostMessage> Messages => incoming;

    /// <summary>
    /// Creates two connected, open channels.
    /// </summary>
    /// <param name="id">The id both ends report.</param>
    /// <returns>The editor's end and the host's end.</returns>
    public static (InMemoryHostChannel Editor, InMemoryHostChannel Host) CreatePair(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var editor = new InMemoryHostChannel(id);
        var host = new InMemoryHostChannel(id);
        editor.peer = host;
        host.peer = editor;
        return (editor, host);
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        InMemoryHostChannel other = peer ?? throw new InvalidOperationException("The channel has no peer.");
        if (State == HostChannelState.Closed || other.State == HostChannelState.Closed)
        {
            throw new InvalidOperationException($"Host channel '{Id}' is closed.");
        }

        other.Deliver(message);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Close();
        peer?.Close();
        return ValueTask.CompletedTask;
    }

    private void Deliver(HostMessage message)
    {
        lock (gate)
        {
            if (State == HostChannelState.Open)
            {
                incoming.OnNext(message);
            }
        }
    }

    private void Close()
    {
        lock (gate)
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                incoming.OnCompleted();
            }
        }
    }
}
