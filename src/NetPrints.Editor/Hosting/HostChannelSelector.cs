using Microsoft.Extensions.Logging;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The channel <see cref="HostChannelSelector.Select"/> chose, and why it is not the requested one, if it is not.
/// </summary>
/// <param name="Channel">The channel to use; the caller owns and disposes it.</param>
/// <param name="Error">Message for the user when a channel was requested but is not available, else <see langword="null"/>.</param>
public sealed record HostChannelSelection(IHostChannel Channel, string? Error);

/// <summary>
/// Chooses the process's host channel from the <c>NETPRINTS_HOST_*</c> environment (extension-points.md §6).
/// </summary>
public static class HostChannelSelector
{
    /// <summary>Prefix of the environment variables that configure the channel.</summary>
    public const string EnvironmentPrefix = "NETPRINTS_HOST_";

    /// <summary>The variable that names the factory (<see cref="EnvironmentPrefix"/> plus <c>CHANNEL</c>).</summary>
    public const string ChannelVariable = EnvironmentPrefix + "CHANNEL";

    /// <summary>
    /// Creates the channel the environment asks for: unset or empty gives <see cref="NullHostChannel.Instance"/>; a
    /// factory id gives that factory's channel, created with every <c>NETPRINTS_HOST_*</c> variable keyed by the name
    /// after the prefix; an id no loaded extension provides (or a factory that throws) gives
    /// <see cref="NullHostChannel.Instance"/> and an error, logged as 1021 when the id is unknown.
    /// </summary>
    /// <param name="registry">The loaded extensions.</param>
    /// <param name="environment">The process environment variables.</param>
    /// <param name="logger">Logger for the unknown-channel failure.</param>
    /// <returns>The channel and the error to show, if any.</returns>
    public static HostChannelSelection Select(ExtensionRegistry registry, IReadOnlyDictionary<string, string> environment, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        if (!environment.TryGetValue(ChannelVariable, out string? id) || string.IsNullOrEmpty(id))
        {
            return new HostChannelSelection(NullHostChannel.Instance, null);
        }

        if (registry.FindHostChannel(id) is not { } factory)
        {
            Log.HostChannelUnknown(logger, id);
            return new HostChannelSelection(NullHostChannel.Instance,
                $"{ChannelVariable} is '{id}', but no loaded extension provides a host channel with that id. The editor runs without a host channel.");
        }

        var settings = environment
            .Where(pair => pair.Key.StartsWith(EnvironmentPrefix, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key[EnvironmentPrefix.Length..], pair => pair.Value, StringComparer.Ordinal);
        try
        {
            return new HostChannelSelection(factory.Create(new HostLaunchContext(settings)), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.HostChannelCreateFailed(logger, ex, id);
            return new HostChannelSelection(NullHostChannel.Instance,
                $"The host channel '{id}' could not be created: {ex.Message} The editor runs without a host channel.");
        }
    }
}
