using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetPrints.Extensibility.Hosting;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Acts on the messages of the host channel on the UI thread (extension-points.md §6): a types-changed message reloads
/// the reflection provider, a focus-document message opens the named document. Any other message, and one that names
/// nothing the editor has open, is ignored and logged at Debug (1022).
/// </summary>
public sealed class HostChannelBridge : IDisposable
{
    private readonly IUiDispatcher dispatcher;
    private readonly Func<Task> reloadTypes;
    private readonly Func<string, string?, bool> focusDocument;
    private readonly ILogger<HostChannelBridge> logger;
    private readonly IDisposable subscription;

    /// <summary>
    /// Subscribes to <paramref name="channel"/>; dispose the bridge to unsubscribe (the channel stays open).
    /// </summary>
    /// <param name="channel">The process's host channel.</param>
    /// <param name="dispatcher">Marshals each message onto the UI thread.</param>
    /// <param name="reloadTypes">Reloads the reflection provider of the open project.</param>
    /// <param name="focusDocument">Opens the document at a path (project-relative or absolute) and optionally a node in it;
    /// returns whether the document is part of the open project.</param>
    /// <param name="logger">Logger for events 1020 and 1022.</param>
    public HostChannelBridge(IHostChannel channel, IUiDispatcher dispatcher, Func<Task> reloadTypes,
        Func<string, string?, bool> focusDocument, ILogger<HostChannelBridge> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.reloadTypes = reloadTypes ?? throw new ArgumentNullException(nameof(reloadTypes));
        this.focusDocument = focusDocument ?? throw new ArgumentNullException(nameof(focusDocument));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        subscription = channel.Messages.Subscribe(message => this.dispatcher.Post(() => Handle(message)));
    }

    private void Handle(HostMessage message)
    {
        Log.HostMessageReceived(logger, message.Type);

        switch (message.Type)
        {
            case HostMessageTypes.TypesChanged:
                reloadTypes().Forget(logger);
                break;

            case HostMessageTypes.FocusDocument:
                if (!TryFocus(message.Payload))
                {
                    Log.HostMessageIgnored(logger, message.Type);
                }

                break;

            default:
                Log.HostMessageIgnored(logger, message.Type);
                break;
        }
    }

    private bool TryFocus(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("path", out JsonElement path) || path.ValueKind != JsonValueKind.String
            || path.GetString() is not { Length: > 0 } documentPath)
        {
            return false;
        }

        string? nodeId = payload.TryGetProperty("nodeId", out JsonElement node) && node.ValueKind == JsonValueKind.String ? node.GetString() : null;
        return focusDocument(documentPath, nodeId);
    }

    /// <summary>Stops listening to the channel.</summary>
    public void Dispose() => subscription.Dispose();
}
