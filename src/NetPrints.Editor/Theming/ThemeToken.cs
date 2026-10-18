using Avalonia.Media;

namespace NetPrints.Editor.Theming;

/// <summary>The kind of value a theme token holds; a theme file writes it as text and the loader parses it by this kind.</summary>
public enum ThemeTokenKind
{
    /// <summary>A <see cref="Avalonia.Media.Color"/>, written <c>#AARRGGBB</c> or <c>#RRGGBB</c>.</summary>
    Color,

    /// <summary>A solid brush, written like a colour.</summary>
    Brush,

    /// <summary>A number (an opacity, a thickness or a spacing).</summary>
    Number,

    /// <summary>A <see cref="Avalonia.Media.BoxShadows"/> list.</summary>
    Shadow,

    /// <summary>A closed set of shapes: <c>triangle</c> or <c>diamond</c>.</summary>
    Shape,
}

/// <summary>One entry of the theme token catalogue (FR-066).</summary>
/// <param name="Name">The resource key, written <c>Area.Role</c>.</param>
/// <param name="Kind">The kind of value the token holds.</param>
/// <param name="PerVariant">Whether the Dark and Light dictionaries of <c>EditorStyles.axaml</c> each hold a value.</param>
public sealed record ThemeToken(string Name, ThemeTokenKind Kind, bool PerVariant = true)
{
    /// <summary>Tells whether a resolved resource has the type this token's kind names.</summary>
    /// <param name="value">The resource value.</param>
    /// <returns><see langword="true"/> when the value fits the kind.</returns>
    public bool Accepts(object? value) => Kind switch
    {
        ThemeTokenKind.Color => value is Color,
        ThemeTokenKind.Brush => value is ISolidColorBrush,
        ThemeTokenKind.Number => value is double,
        ThemeTokenKind.Shadow => value is BoxShadows,
        ThemeTokenKind.Shape => value is Geometry,
        _ => false,
    };
}
