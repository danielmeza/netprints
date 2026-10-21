using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
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

    /// <summary>A <see cref="Avalonia.Thickness"/>, written <c>4</c>, <c>8,2</c> or <c>1,0,1,0</c>.</summary>
    Thickness,

    /// <summary>A <see cref="Avalonia.CornerRadius"/>, written <c>4</c> or <c>4,4,0,0</c>.</summary>
    CornerRadius,

    /// <summary>A <see cref="Avalonia.Controls.GridLength"/>, written <c>120</c>, <c>Auto</c> or <c>2*</c>.</summary>
    GridLength,

    /// <summary>A font family name such as <c>Cascadia Mono</c>; a URI or a font file is refused (ADR-0035 decision 8).</summary>
    FontFamily,

    /// <summary>A <see cref="System.TimeSpan"/> of zero or more, written <c>0:0:0.15</c>.</summary>
    Duration,

    /// <summary>A cubic Bézier easing, written <c>x1,y1,x2,y2</c> with x values from 0 to 1.</summary>
    Easing,
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
        ThemeTokenKind.Thickness => value is Thickness,
        ThemeTokenKind.CornerRadius => value is CornerRadius,
        ThemeTokenKind.GridLength => value is GridLength,
        ThemeTokenKind.FontFamily => value is FontFamily,
        ThemeTokenKind.Duration => value is TimeSpan,
        ThemeTokenKind.Easing => value is Easing,
        _ => false,
    };
}
