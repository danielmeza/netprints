using System.Globalization;
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
    /// <param name="value">The parsed value: a <see cref="Color"/>, a solid brush, a <see cref="double"/>, a <see cref="BoxShadows"/> or the shape name; <see langword="null"/> when the text is invalid.</param>
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
            ThemeTokenKind.Shadow => ParseShadows(text),
            ThemeTokenKind.Shape => text is Triangle or Diamond ? text : null,
            _ => null,
        };
        return value is not null;
    }

    private static object? ParseShadows(string text)
    {
        try
        {
            return BoxShadows.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
