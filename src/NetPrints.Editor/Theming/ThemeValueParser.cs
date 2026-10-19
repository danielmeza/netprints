using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NetPrints.Editor.Theming;

/// <summary>Parses the text of a theme token by the kind the catalogue gives it.</summary>
public static class ThemeValueParser
{
    /// <summary>The text of the triangle pin shape.</summary>
    public const string Triangle = "triangle";

    /// <summary>The text of the diamond pin shape.</summary>
    public const string Diamond = "diamond";

    /// <summary>Parses a value.</summary>
    /// <param name="token">The token the value is for.</param>
    /// <param name="text">The text in the theme file.</param>
    /// <param name="value">The parsed value: a <see cref="Color"/>, a solid brush, a <see cref="double"/>, a <see cref="BoxShadows"/>, the shape name, a <see cref="Thickness"/>, a <see cref="CornerRadius"/>, a <see cref="GridLength"/>, a <see cref="FontFamily"/>, a <see cref="TimeSpan"/> or an <see cref="Easing"/>; <see langword="null"/> when the text is invalid.</param>
    /// <returns><see langword="true"/> when the text is a valid value for the kind.</returns>
    public static bool TryParse(ThemeToken token, string text, out object? value)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(text);
        value = token.Kind switch
        {
            ThemeTokenKind.Color => Color.TryParse(text, out Color color) ? color : null,
            ThemeTokenKind.Brush => Color.TryParse(text, out Color fill) ? new ImmutableSolidColorBrush(fill) : null,
            ThemeTokenKind.Number => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) ? number : null,
            ThemeTokenKind.Shadow => Parse(() => BoxShadows.Parse(text)),
            ThemeTokenKind.Shape => text is Triangle or Diamond ? text : null,
            ThemeTokenKind.Thickness => Parse(() => Thickness.Parse(text)),
            ThemeTokenKind.CornerRadius => Parse(() => CornerRadius.Parse(text)),
            ThemeTokenKind.GridLength => Parse(() => GridLength.Parse(text)),
            ThemeTokenKind.FontFamily => IsFamilyName(text) ? new FontFamily(text) : null,
            ThemeTokenKind.Duration => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan span) && span >= TimeSpan.Zero ? span : null,
            ThemeTokenKind.Easing => Parse(() => new SplineEasing(KeySpline.Parse(text, CultureInfo.InvariantCulture))),
            _ => null,
        };
        return value is not null;
    }

    private static object? Parse(Func<object> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static bool IsFamilyName(string text) =>
        text.Trim().Length > 0 && !text.Contains("://", StringComparison.Ordinal) && !text.Contains('#', StringComparison.Ordinal) && !text.Contains('/', StringComparison.Ordinal);
}
