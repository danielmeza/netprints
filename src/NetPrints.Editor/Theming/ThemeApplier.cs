using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;
using Semi.Avalonia;

namespace NetPrints.Editor.Theming;

/// <summary>Applies the <c>netprints.editor</c> appearance keys to the application: both variants' presets as overlays, then the variant to run on (FR-069).</summary>
/// <param name="application">The application to theme.</param>
/// <param name="logger">Receives one entry for each preset id that names no preset.</param>
public sealed class ThemeApplier(Application application, ILogger logger)
{
    private readonly ThemeOverlay overlay = new(application);

    /// <summary>Applies the settings; a preset id that names no preset of its variant falls back to the variant's default, with a log entry, and the other keys still apply.</summary>
    /// <param name="settings">The appearance section.</param>
    public void Apply(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThemePreset dark = Resolve(ThemeBase.Dark, settings.DarkPreset);
        ThemePreset light = Resolve(ThemeBase.Light, settings.LightPreset);
        overlay.Apply(ThemeBase.Dark, dark);
        overlay.Apply(ThemeBase.Light, light);
        application.RequestedThemeVariant = settings.Theme switch
        {
            EditorTheme.Dark => VariantOf(dark, ThemeVariant.Dark),
            EditorTheme.Light => VariantOf(light, ThemeVariant.Light),
            _ => ThemeVariant.Default,
        };
    }

    private static ThemeVariant VariantOf(ThemePreset preset, ThemeVariant plain) => preset.SemiVariant switch
    {
        nameof(SemiTheme.Aquatic) => SemiTheme.Aquatic,
        nameof(SemiTheme.Dusk) => SemiTheme.Dusk,
        nameof(SemiTheme.Desert) => SemiTheme.Desert,
        nameof(SemiTheme.NightSky) => SemiTheme.NightSky,
        _ => plain,
    };

    private ThemePreset Resolve(ThemeBase variant, string id)
    {
        ThemePreset preset = ThemePresets.Resolve(variant, id, out bool found);
        if (!found)
        {
            Hosting.Log.ThemePresetUnknown(logger, id, variant.ToString(), preset.Id);
        }

        return preset;
    }
}
