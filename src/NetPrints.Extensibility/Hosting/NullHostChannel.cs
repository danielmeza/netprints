using System.Reactive.Linq;

namespace NetPrints.Extensibility.Hosting;

/// <summary>
/// The channel used when no host is attached: open, silent, and it drops what is sent.
/// </summary>
public sealed class NullHostChannel : IHostChannel
{
    private NullHostChannel()
    {
    }

    /// <summary>
    /// The shared instance.
    /// </summary>
    public static NullHostChannel Instance { get; } = new();

    /// <inheritdoc/>
    public string Id => "null";

    /// <inheritdoc/>
    public HostChannelState State => HostChannelState.Open;

    /// <inheritdoc/>
    public IObservable<HostMessage> Messages { get; } = Observable.Never<HostMessage>();

    /// <inheritdoc/>
    public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
