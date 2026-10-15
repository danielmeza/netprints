using System.Text.Json;

namespace NetPrints.Editor.State;

/// <summary>The content of <c>layout.json</c>: the ADR-0018 envelope around the layout the docking adapter owns.</summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="Engine">The layout engine, <c>dock</c> or <c>grid</c>.</param>
/// <param name="DockLayout">The adapter's layout tree, opaque to the store.</param>
public sealed record LayoutState(int SchemaVersion, string Engine, JsonElement? DockLayout) : IStateFile;
