using NetPrints.Graph;

namespace NetPrints.Editor.Events;

/// <summary>Requests that the editor selects an event entry on the canvas of its graph, which shows the entry inspector.</summary>
/// <param name="Entry">The entry to select.</param>
public sealed record SelectEventEntryMessage(EventEntryNode Entry);
