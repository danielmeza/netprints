using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;

namespace Fx.Fail.HostChannel;

/// <summary>A channel that never closes: its state stays open when it is disposed.</summary>
public sealed class StuckChannel : IHostChannel, IObservable<HostMessage>
{
    /// <inheritdoc />
    public string Id => FailHostChannelFactory.FactoryId;

    /// <inheritdoc />
    public HostChannelState State => HostChannelState.Open;

    /// <inheritdoc />
    public IObservable<HostMessage> Messages => this;

    /// <inheritdoc />
    public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<HostMessage> observer) => new Subscription();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class Subscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>The factory of the stuck channel.</summary>
public sealed class FailHostChannelFactory : IHostChannelFactory
{
    /// <summary>The factory id.</summary>
    public const string FactoryId = "fx.fail.hostchannel";

    /// <inheritdoc />
    public string Id => FactoryId;

    /// <inheritdoc />
    public IHostChannel Create(HostLaunchContext context) => new StuckChannel();
}

/// <summary>An extension whose host channel does not close.</summary>
public sealed class FailHostChannelExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddHostChannel(new FailHostChannelFactory());
    }
}
