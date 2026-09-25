using System.Text.Json.Serialization;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// A query for UI elements by automation id, optionally narrowed by <c>AutomationProperties.Name</c>,
/// by text, and by an enclosing element. Part of the read-only automation protocol.
/// </summary>
public sealed record AutomationQuery(string AutomationId)
{
    /// <summary>Only elements whose <c>AutomationProperties.Name</c> equals this value.</summary>
    public string? Name { get; init; }

    /// <summary>Only elements whose text (Text, Content, Title, …) equals this value.</summary>
    public string? Text { get; init; }

    /// <summary>Only descendants of the elements matched by this query.</summary>
    public AutomationQuery? Within { get; init; }

    /// <summary>Include elements that are not effectively visible.</summary>
    public bool IncludeHidden { get; init; }

    /// <summary>Only the match at this position (in tree order), when several elements match.</summary>
    public int? Index { get; init; }

    /// <summary>
    /// Returns this query's <c>&gt;</c>-chained <see cref="Within"/> ancestor(s) followed by its
    /// <see cref="AutomationId"/>, with any set <see cref="Name"/>, <see cref="Text"/> and
    /// <see cref="Index"/> appended in brackets (eg. <c>"Graph.Editor &gt; Graph.Node[name=Foo][0]"</c>).
    /// </summary>
    /// <returns>The query's display string.</returns>
    public override string ToString() =>
        (Within is null ? "" : $"{Within} > ") + AutomationId
        + (Name is null ? "" : $"[name={Name}]") + (Text is null ? "" : $"[text={Text}]") + (Index is null ? "" : $"[{Index}]");
}

/// <summary>A rectangle; client bounds are in DIPs, screen bounds in device pixels.</summary>
public sealed record AutomationRect(double X, double Y, double Width, double Height)
{
    /// <summary>The X coordinate of the rectangle's center.</summary>
    [JsonIgnore]
    public double CenterX => X + Width / 2;

    /// <summary>The Y coordinate of the rectangle's center.</summary>
    [JsonIgnore]
    public double CenterY => Y + Height / 2;
}

/// <summary>A snapshot of one UI element.</summary>
/// <param name="AutomationId">The element's automation id (see <see cref="AutomationIds"/>), or "" if it has none.</param>
/// <param name="Name">The element's <c>AutomationProperties.Name</c>, or <see langword="null"/> if it has none.</param>
/// <param name="Window">Key of the window that contains the element (popups belong to their owning window).</param>
/// <param name="Bounds">Bounds relative to the window's client area, in DIPs.</param>
/// <param name="ScreenBounds">Bounds on the screen, in device pixels.</param>
/// <param name="Properties">Read-only properties (Type, Text, IsEnabled, IsVisible, PseudoClasses, …).</param>
public sealed record AutomationElement(
    string AutomationId,
    string? Name,
    string Window,
    AutomationRect Bounds,
    AutomationRect ScreenBounds,
    IReadOnlyDictionary<string, string?> Properties)
{
    /// <summary>
    /// Looks up one of <see cref="Properties"/> by name (see <see cref="AutomationPropertyNames"/>).
    /// </summary>
    /// <param name="property">Name of the property to look up.</param>
    /// <returns>The property's value, or <see langword="null"/> if it is not present.</returns>
    [JsonIgnore]
    public string? this[string property] => Properties.TryGetValue(property, out var value) ? value : null;

    /// <summary>The element's <see cref="AutomationPropertyNames.Text"/> property.</summary>
    [JsonIgnore]
    public string? Text => this[AutomationPropertyNames.Text];

    /// <summary>The element's <see cref="AutomationPropertyNames.IsEnabled"/> property.</summary>
    [JsonIgnore]
    public bool IsEnabled => this[AutomationPropertyNames.IsEnabled] == "True";

    /// <summary>The element's <see cref="AutomationPropertyNames.IsVisible"/> property.</summary>
    [JsonIgnore]
    public bool IsVisible => this[AutomationPropertyNames.IsVisible] == "True";

    /// <summary>Whether a pseudo-class such as ":pointerover" or ":pressed" is set.</summary>
    public bool Has(string pseudoClass) => (this[AutomationPropertyNames.PseudoClasses] ?? "").Split(' ').Contains(pseudoClass);
}
