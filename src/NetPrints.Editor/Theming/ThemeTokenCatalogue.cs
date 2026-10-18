using System.Collections.Frozen;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Theming;

/// <summary>
/// The theme token catalogue (FR-066, ADR-0035 decision 1): every colour, shadow, shape, opacity and thickness a theme can set.
/// Sizes, fonts and spacing that no variant changes stay plain resources until user themes (B1) widen the catalogue.
/// </summary>
public static class ThemeTokenCatalogue
{
    private static readonly string[] BrushNames =
    [
        "StartPage.RecentTileBackground", "Focus.Ring", "State.Hover", "State.Pressed", "Row.Separator", "Tab.SelectedAccent",
        "GraphWatermark.Foreground", "Node.CardBackground", "Node.TitleForeground", "Node.SubtitleForeground",
        "OpeningGraphIndicator.Background", "OpeningGraphIndicator.Foreground", "BusyOverlay.Background", "Popup.Background",
        "Pin.HoverStroke", "Diagnostic.Error.Foreground", "Diagnostic.Warning.Foreground", "Diagnostic.Info.Foreground",
        "CommandBar.BadgeBackground", "CommandBar.BadgeForeground", "Icon.Method.Foreground", "Icon.Property.Foreground",
        "Icon.Event.Foreground", "Icon.Class.Foreground", "Node.HeaderForeground", "Node.Border", "Pin.DefaultValue", "Pin.Hint",
        "Canvas.SelectionBorder", "Canvas.MarqueeFill", "Canvas.MarqueeBorder", "Canvas.WireSelected",
        "Inspector.CategoryHeaderBackground", "Inspector.CategoryHeaderForeground", "Inspector.ModifiedMarker",
        "Inspector.EditorBackground", "Inspector.EditorBorder",
    ];

    private static readonly string[] ColorNames = ["Canvas.Background", "Tab.StripBorder", "GraphGrid.MinorColor", "GraphGrid.MajorColor"];

    private static readonly string[] ShadowNames = ["Node.CardShadow", "Popup.Shadow"];

    private static readonly string[] NumberNames = ["Wire.Opacity", "Wire.Thickness", "Inspector.RowSpacing"];

    /// <summary>Gets the key of the type-pin shape token.</summary>
    public const string PinShapeToken = "Pin.TypeShape";

    /// <summary>Gets every token of the catalogue.</summary>
    public static IReadOnlyList<ThemeToken> All { get; } =
    [
        .. ColorNames.Select(name => new ThemeToken(name, ThemeTokenKind.Color)),
        .. BrushNames.Select(name => new ThemeToken(name, ThemeTokenKind.Brush)),
        .. NodeRole.All.Select(role => new ThemeToken("Node.Header." + role.Name, ThemeTokenKind.Brush)),
        .. PinTypeFamily.All.Select(family => new ThemeToken(family.TokenKey, ThemeTokenKind.Brush)),
        .. ShadowNames.Select(name => new ThemeToken(name, ThemeTokenKind.Shadow)),
        .. NumberNames.Select(name => new ThemeToken(name, ThemeTokenKind.Number)),
        new ThemeToken(PinShapeToken, ThemeTokenKind.Shape, PerVariant: false),
        new ThemeToken("Pin.OutlineThickness", ThemeTokenKind.Number, PerVariant: false),
    ];

    private static readonly FrozenDictionary<string, ThemeToken> ByName = All.ToFrozenDictionary(token => token.Name, StringComparer.Ordinal);

    /// <summary>Finds a token by its resource key.</summary>
    /// <param name="name">The resource key.</param>
    /// <returns>The token, or <see langword="null"/> when the catalogue has none of that name.</returns>
    public static ThemeToken? Find(string name) => ByName.GetValueOrDefault(name);
}
