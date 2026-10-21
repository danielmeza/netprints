namespace NetPrints.Editor.Theming;

/// <summary>The application theme variant a preset is laid over.</summary>
public enum ThemeBase
{
    /// <summary>The Dark variant.</summary>
    Dark,

    /// <summary>The Light variant.</summary>
    Light,
}

/// <summary>A theme read from a <c>*.nptheme.json</c> file: a sparse set of token values over the XAML of one variant (ADR-0035).</summary>
/// <param name="Id">The id the <c>darkPreset</c> and <c>lightPreset</c> settings name.</param>
/// <param name="Label">The name the Appearance page shows.</param>
/// <param name="Base">The variant the preset is laid over.</param>
/// <param name="SemiVariant">The name of the Semi high-contrast variant the preset runs on (<c>Aquatic</c>, <c>Dusk</c>, <c>Desert</c> or <c>NightSky</c>), or <see langword="null"/> for the plain variant.</param>
/// <param name="Tokens">The token values, as text keyed by token name; every entry has been validated against the catalogue.</param>
public sealed record ThemePreset(string Id, string Label, ThemeBase Base, string? SemiVariant, IReadOnlyDictionary<string, string> Tokens);

/// <summary>What loading one theme file gave.</summary>
/// <param name="Preset">The theme, or <see langword="null"/> when the file was skipped.</param>
/// <param name="Warnings">One message per problem; each names the token or the reason.</param>
public sealed record ThemeLoadResult(ThemePreset? Preset, IReadOnlyList<string> Warnings);
