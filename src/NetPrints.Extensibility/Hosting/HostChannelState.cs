namespace NetPrints.Extensibility.Hosting;

/// <summary>
/// Where a host channel is in its life (extension-points.md §6).
/// </summary>
public enum HostChannelState
{
    /// <summary>The channel is being established; messages cannot be exchanged yet.</summary>
    Connecting,

    /// <summary>Messages can be sent and are received.</summary>
    Open,

    /// <summary>The channel ended; <see cref="IHostChannel.Messages"/> completed and sending fails.</summary>
    Closed,
}
