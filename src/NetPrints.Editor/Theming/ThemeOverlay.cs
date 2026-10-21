using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace NetPrints.Editor.Theming;

/// <summary>
/// Writes a preset into <c>Application.Resources.ThemeDictionaries[Dark|Light]</c> (FR-067), where it beats the XAML tokens of
/// <c>EditorStyles.axaml</c>; a flat key in <c>Application.Resources</c> would beat both variants, so a Dark preset never reaches Light.
/// </summary>
/// <param name="application">The application whose resources receive the overlay.</param>
public sealed class ThemeOverlay(Application application)
{
    private const string ShapePrefix = "Pin.TypeShape.";
    private const string TriangleKey = ShapePrefix + "Triangle";
    private const string DiamondKey = ShapePrefix + "Diamond";

    /// <summary>Replaces the overlay of a variant with a preset; the keys the previous preset set and this one does not return to the XAML values.</summary>
    /// <param name="variant">The variant the overlay is for.</param>
    /// <param name="preset">The preset to apply.</param>
    public void Apply(ThemeBase variant, ThemePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ThemeVariant theme = VariantOf(variant);
        ResourceDictionary overlay = OverlayOf(theme);
        Dictionary<string, object> next = new(StringComparer.Ordinal);
        foreach ((string name, string text) in preset.Tokens)
        {
            if (ThemeTokenCatalogue.Find(name) is { } token && ThemeValueParser.TryParse(token, text, out object? value) && value is not null)
            {
                object? resolved = token.Kind == ThemeTokenKind.Shape ? ShapeOf(theme, text) : value;
                if (resolved is not null)
                {
                    next[name] = resolved;
                }
            }
        }

        foreach (string stale in overlay.Keys.OfType<string>().Where(key => !next.ContainsKey(key)).ToList())
        {
            overlay.Remove(stale);
        }

        foreach ((string name, object value) in next)
        {
            overlay[name] = value;
        }
    }

    /// <summary>Empties the overlays of both variants.</summary>
    public void Clear()
    {
        foreach (ThemeVariant theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            OverlayOf(theme).Clear();
        }
    }

    private static ThemeVariant VariantOf(ThemeBase variant) => variant == ThemeBase.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    private object? ShapeOf(ThemeVariant theme, string text) =>
        application.TryGetResource(text == ThemeValueParser.Triangle ? TriangleKey : DiamondKey, theme, out object? geometry) ? geometry : null;

    private ResourceDictionary OverlayOf(ThemeVariant theme)
    {
        if (application.Resources.ThemeDictionaries.TryGetValue(theme, out IThemeVariantProvider? existing) && existing is ResourceDictionary dictionary)
        {
            return dictionary;
        }

        var created = new ResourceDictionary();
        application.Resources.ThemeDictionaries[theme] = created;
        return created;
    }
}
