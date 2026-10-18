using System.Collections.Immutable;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using NetPrints.Editor.State;
using NetPrints.Editor.Theming;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>One colour of a preview strip.</summary>
/// <param name="Token">The theme token the colour comes from.</param>
/// <param name="Brush">The colour as the preset gives it, whatever theme is running.</param>
internal sealed record ThemeSwatch(string Token, IBrush Brush);

/// <summary>The preview of the preset one variant has chosen.</summary>
/// <param name="Variant">The variant's name.</param>
/// <param name="PresetLabel">The label of the preset that applies, which is the variant's default when the setting names no preset.</param>
/// <param name="Swatches">The colours of the strip.</param>
internal sealed record ThemePreviewRow(string Variant, string PresetLabel, ImmutableArray<ThemeSwatch> Swatches);

/// <summary>Builds the rows of the Appearance preview from the settings and the tokens of the running application.</summary>
internal static class ThemePreviewRows
{
    private const string CanvasToken = "Canvas.Background";
    private const string CardToken = "Node.CardBackground";

    /// <summary>Gets the tokens the strip shows, in order.</summary>
    public static IReadOnlyList<string> SwatchTokens { get; } =
        [CanvasToken, CardToken, "Pin.Exec", "Pin.Bool", "Pin.Integer", "Pin.String", "Canvas.WireSelected"];

    /// <summary>Builds the Dark and the Light row.</summary>
    /// <param name="store">The settings the chosen presets are read from.</param>
    /// <param name="baseLookup">Resolves a token in the XAML of a variant.</param>
    /// <returns>The two rows.</returns>
    public static ImmutableArray<ThemePreviewRow> Build(ISettingsStore store, Func<string, ThemeBase, object?> baseLookup)
    {
        EditorSettings settings = store.Get(EditorSettings.Descriptor);
        return
        [
            Row(ThemeBase.Dark, Strings.Settings_ThemePreview_Dark, settings.DarkPreset, baseLookup),
            Row(ThemeBase.Light, Strings.Settings_ThemePreview_Light, settings.LightPreset, baseLookup),
        ];
    }

    /// <summary>Resolves a token in the running application's resources for a variant.</summary>
    /// <param name="name">The token name.</param>
    /// <param name="variant">The variant.</param>
    /// <returns>The value, or <see langword="null"/> when there is no application or no such resource.</returns>
    public static object? FromApplication(string name, ThemeBase variant) =>
        Application.Current is { } app && app.TryGetResource(name, variant == ThemeBase.Dark ? ThemeVariant.Dark : ThemeVariant.Light, out object? value) ? value : null;

    /// <summary>Runs an action on the UI thread.</summary>
    /// <param name="action">The action.</param>
    public static void PostToUi(Action action) => Dispatcher.UIThread.Post(action);

    private static ThemePreviewRow Row(ThemeBase variant, string name, string presetId, Func<string, ThemeBase, object?> baseLookup)
    {
        ThemePreset preset = ThemePresets.Resolve(variant, presetId, out _);
        var palette = new ThemePalette(preset, token => baseLookup(token, variant));
        Color canvas = palette.TryColour(CanvasToken) ?? Colors.Transparent;
        Color Shown(string token)
        {
            Color color = palette.TryColour(token) ?? Colors.Transparent;
            return token == CardToken ? ContrastMath.Composite(color, canvas) : color;
        }

        ImmutableArray<ThemeSwatch> swatches = [.. SwatchTokens.Select(token => new ThemeSwatch(token, new ImmutableSolidColorBrush(Shown(token))))];
        return new ThemePreviewRow(name, preset.Label, swatches);
    }
}
