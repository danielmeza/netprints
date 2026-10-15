namespace NetPrints.Editor.Shell.Docking;

/// <summary>The persisted dock layout (ADR-0018): the main window's tree, the hidden panels and the floating windows.</summary>
/// <param name="Root">The main window's layout, below the root dock.</param>
/// <param name="Hidden">The ids of the panels the user hid.</param>
/// <param name="Windows">The floating windows.</param>
/// <param name="ActiveDocument">The id of the active document, or <see langword="null"/> for none.</param>
internal sealed record DockLayoutDto(DockNodeDto Root, IReadOnlyList<string>? Hidden = null, IReadOnlyList<FloatingWindowDto>? Windows = null, string? ActiveDocument = null);

/// <summary>One node of the persisted dock tree.</summary>
/// <param name="Kind">One of <see cref="DockNodeKinds"/>.</param>
/// <param name="Id">The dock, panel or document id.</param>
/// <param name="Proportion">The share of the parent, or <see langword="null"/> for none.</param>
/// <param name="Orientation">For a proportional dock, <c>horizontal</c> or <c>vertical</c>.</param>
/// <param name="Alignment">For a tool dock, the edge it sits on.</param>
/// <param name="ActiveId">For a tool or document dock, the id of the active child.</param>
/// <param name="Children">The children, in order.</param>
internal sealed record DockNodeDto(
    string Kind,
    string Id,
    double? Proportion = null,
    string? Orientation = null,
    string? Alignment = null,
    string? ActiveId = null,
    IReadOnlyList<DockNodeDto>? Children = null);

/// <summary>A floating window.</summary>
/// <param name="X">Left edge, in screen pixels.</param>
/// <param name="Y">Top edge, in screen pixels.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="Root">The window's layout.</param>
internal sealed record FloatingWindowDto(double X, double Y, double Width, double Height, DockNodeDto Root);

/// <summary>The node kinds of <see cref="DockNodeDto.Kind"/>.</summary>
internal static class DockNodeKinds
{
    /// <summary>A split of docks and splitters.</summary>
    public const string Proportional = "proportional";

    /// <summary>A dock of panels.</summary>
    public const string Tools = "tools";

    /// <summary>A dock of documents.</summary>
    public const string Documents = "documents";

    /// <summary>The bar between two docks.</summary>
    public const string Splitter = "splitter";

    /// <summary>A panel.</summary>
    public const string Tool = "tool";

    /// <summary>A document.</summary>
    public const string Document = "document";
}
