using System.Text.Json;

namespace NetPrints.Extensibility.Hosting;

/// <summary>
/// One message on a host channel: a type id and a JSON payload.
/// </summary>
/// <param name="Type">The message type, for example <see cref="HostMessageTypes.TypesChanged"/>.</param>
/// <param name="Payload">The payload; its shape depends on <paramref name="Type"/>.</param>
public sealed record HostMessage(string Type, JsonElement Payload);

/// <summary>
/// The message types the editor understands (extension-points.md §6). Others are ignored.
/// </summary>
public static class HostMessageTypes
{
    /// <summary>The host's types changed; the editor reloads reflection. Payload: <c>{}</c>.</summary>
    public const string TypesChanged = "netprints.types-changed";

    /// <summary>The host asks the editor to open a document. Payload: <c>{ "path": "...", "nodeId"?: "n3" }</c>.</summary>
    public const string FocusDocument = "netprints.focus-document";
}
