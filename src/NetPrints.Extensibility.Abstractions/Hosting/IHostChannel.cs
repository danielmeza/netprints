using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
namespace NetPrints.Extensibility.Hosting;

/// <summary>
/// A two-way message channel between the editor and the application that hosts it (extension-points.md §6).
/// </summary>
[Experimental(ExperimentalApiIds.HostChannel, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IHostChannel : IAsyncDisposable
{
    /// <summary>
    /// The id of the factory that created the channel.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The current state.
    /// </summary>
    HostChannelState State { get; }

    /// <summary>
    /// The messages from the host. May emit on any thread. Completes when the channel closes and never calls
    /// <c>OnError</c>.
    /// </summary>
    IObservable<HostMessage> Messages { get; }

    /// <summary>
    /// Sends a message to the host.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message was handed to the host.</returns>
    /// <exception cref="InvalidOperationException">The channel is closed.</exception>
    ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Launch-time settings for a channel.
/// </summary>
/// <param name="Settings">The <c>NETPRINTS_HOST_*</c> environment variables, keyed by the name after the prefix.</param>
public sealed record HostLaunchContext(IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// Creates the channel selected by <c>NETPRINTS_HOST_CHANNEL</c> (extension-points.md §6).
/// </summary>
[Experimental(ExperimentalApiIds.HostChannel, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IHostChannelFactory
{
    /// <summary>
    /// The factory id the environment variable names; unique across loaded extensions.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Creates the channel.
    /// </summary>
    /// <param name="context">Launch settings.</param>
    /// <returns>The channel; the caller disposes it.</returns>
    IHostChannel Create(HostLaunchContext context);
}
