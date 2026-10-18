using Avalonia.Media;

namespace NetPrints.Editor.Theming;

/// <summary>The value of every catalogue token under one preset: the preset's own values, and the base variant's for the rest.</summary>
public sealed class ThemePalette
{
    private readonly Dictionary<string, object> values = new(StringComparer.Ordinal);

    /// <summary>Creates the palette of a preset.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="baseLookup">Resolves a token in the XAML of the preset's variant, or returns <see langword="null"/> when it has none.</param>
    public ThemePalette(ThemePreset preset, Func<string, object?> baseLookup)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(baseLookup);
        foreach (ThemeToken token in ThemeTokenCatalogue.All)
        {
            if (preset.Tokens.TryGetValue(token.Name, out string? text) && ThemeValueParser.TryParse(token, text, out object? own) && own is not null)
            {
                values[token.Name] = own;
            }
            else if (baseLookup(token.Name) is { } inherited)
            {
                values[token.Name] = inherited;
            }
        }
    }

    /// <summary>Reads a colour or solid-brush token.</summary>
    /// <param name="name">The token name.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="InvalidOperationException">The token has no colour value.</exception>
    public Color Colour(string name) => TryColour(name) ?? throw new InvalidOperationException($"The token {name} has no colour.");

    /// <summary>Reads a colour or solid-brush token that may have no value.</summary>
    /// <param name="name">The token name.</param>
    /// <returns>The colour, or <see langword="null"/> when neither the preset nor the base variant gives one.</returns>
    public Color? TryColour(string name) => values.GetValueOrDefault(name) switch
    {
        Color color => color,
        ISolidColorBrush brush => brush.Color,
        _ => null,
    };

    /// <summary>Reads a number token.</summary>
    /// <param name="name">The token name.</param>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidOperationException">The token has no number value.</exception>
    public double Number(string name) =>
        values.GetValueOrDefault(name) is double number ? number : throw new InvalidOperationException($"The token {name} has no number.");
}
