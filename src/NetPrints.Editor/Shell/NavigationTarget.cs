namespace NetPrints.Editor.Shell;

/// <summary>A place to navigate to: a document, optionally an item in it.</summary>
/// <param name="Document">The document to open.</param>
/// <param name="NodeId">The node to centre and select, or null.</param>
/// <param name="PinId">The pin to select, or null.</param>
public sealed record NavigationTarget(DocumentId Document, string? NodeId = null, string? PinId = null);
