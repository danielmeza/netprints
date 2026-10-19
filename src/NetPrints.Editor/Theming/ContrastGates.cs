using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Theming;

/// <summary>One contrast pair a preset was measured on.</summary>
/// <param name="Gate">The gate the pair belongs to.</param>
/// <param name="Pair">The two tokens, as text.</param>
/// <param name="Ratio">The measured contrast ratio.</param>
/// <param name="Minimum">The ratio the gate requires.</param>
public sealed record ContrastCheck(string Gate, string Pair, double Ratio, double Minimum)
{
    /// <summary>Gets a value indicating whether the measured ratio reaches the minimum; it is not rounded (WCAG thresholds are exact).</summary>
    public bool Passed => Ratio >= Minimum;
}

/// <summary>The contrast gates every built-in preset passes (FR-068, SC-009); a user theme that fails them is applied with a warning (ADR-0035 decision 4).</summary>
public static class ContrastGates
{
    /// <summary>The gate of the header text against each node role.</summary>
    public const string HeaderGate = "header";

    /// <summary>The gate of each pin colour against the node body and the canvas.</summary>
    public const string PinGate = "pin";

    /// <summary>The gate of each wire, drawn at its opacity, against the canvas and the node body.</summary>
    public const string WireGate = "wire";

    /// <summary>The gate of the card against the canvas.</summary>
    public const string CardGate = "card";

    /// <summary>The gate of the node title against the node body.</summary>
    public const string TitleGate = "title";

    /// <summary>The ratio WCAG 2.x asks of text.</summary>
    public const double TextMinimum = 4.5;

    /// <summary>The ratio WCAG 2.x asks of non-text graphics.</summary>
    public const double GraphicMinimum = 3.0;

    /// <summary>The ratio at which a card still reads as separate from the canvas.</summary>
    public const double CardSeparationMinimum = 1.15;

    private const string CanvasToken = "Canvas.Background";
    private const string CardToken = "Node.CardBackground";

    /// <summary>Measures every gated pair of a palette.</summary>
    /// <param name="palette">The palette of a preset.</param>
    /// <returns>One check per pair, passed or not.</returns>
    public static IReadOnlyList<ContrastCheck> Evaluate(ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var canvas = palette.Colour(CanvasToken);
        var body = ContrastMath.Composite(palette.Colour(CardToken), canvas);
        double opacity = palette.Number("Wire.Opacity");
        List<ContrastCheck> checks = [];

        void Add(string gate, string pair, Avalonia.Media.Color a, Avalonia.Media.Color b, double minimum) =>
            checks.Add(new ContrastCheck(gate, pair, ContrastMath.Ratio(a, b), minimum));

        var headerText = palette.Colour("Node.HeaderForeground");
        foreach (NodeRole role in NodeRole.All)
        {
            string token = "Node.Header." + role.Name;
            Add(HeaderGate, $"{token} on Node.HeaderForeground", palette.Colour(token), headerText, TextMinimum);
        }

        foreach (PinTypeFamily family in PinTypeFamily.All)
        {
            var pin = palette.Colour(family.TokenKey);
            Add(PinGate, $"{family.TokenKey} on the node body", pin, body, GraphicMinimum);
            Add(PinGate, $"{family.TokenKey} on {CanvasToken}", pin, canvas, GraphicMinimum);
            var wire = ContrastMath.WithOpacity(pin, opacity);
            Add(WireGate, $"{family.TokenKey} wire on {CanvasToken}", ContrastMath.Composite(wire, canvas), canvas, GraphicMinimum);
            Add(WireGate, $"{family.TokenKey} wire on the node body", ContrastMath.Composite(wire, body), body, GraphicMinimum);
        }

        Add(CardGate, $"{CardToken} on {CanvasToken}", body, canvas, CardSeparationMinimum);
        Add(TitleGate, "Node.TitleForeground on the node body", palette.Colour("Node.TitleForeground"), body, TextMinimum);
        return checks;
    }
}
