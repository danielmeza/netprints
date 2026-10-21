using System.Collections.Frozen;
using System.Reflection;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Theming;

/// <summary>The presets embedded in the editor (FR-068): NetPrints Dark, NetPrints Light and High contrast for each variant.</summary>
public static class ThemePresets
{
    private const string ResourcePrefix = "NetPrints.Editor.Theming.Presets.";
    private const string ResourceSuffix = ".nptheme.json";

    private static readonly (IReadOnlyList<ThemePreset> Presets, IReadOnlyList<string> Warnings) Loaded = Load();

    private static readonly FrozenDictionary<ThemeBase, string[]> Ids = new Dictionary<ThemeBase, string[]>
    {
        [ThemeBase.Dark] = Order(ThemeBase.Dark),
        [ThemeBase.Light] = Order(ThemeBase.Light),
    }.ToFrozenDictionary();

    /// <summary>Gets every embedded preset.</summary>
    public static IReadOnlyList<ThemePreset> BuiltIn => Loaded.Presets;

    /// <summary>Gets the warnings loading the embedded files gave; empty when the files are sound, which a test pins.</summary>
    public static IReadOnlyList<string> Warnings => Loaded.Warnings;

    /// <summary>Lists the ids a variant offers, its default first.</summary>
    /// <param name="variant">The variant.</param>
    /// <returns>The preset ids.</returns>
    public static IReadOnlyList<string> IdsFor(ThemeBase variant) => Ids[variant];

    /// <summary>Gets the id of a variant's default preset.</summary>
    /// <param name="variant">The variant.</param>
    /// <returns>The id.</returns>
    public static string DefaultId(ThemeBase variant) => variant == ThemeBase.Dark ? EditorSettings.DefaultDarkPreset : EditorSettings.DefaultLightPreset;

    /// <summary>Finds the preset a setting names, or the default of the variant.</summary>
    /// <param name="variant">The variant the setting is for; a preset laid over the other variant counts as unknown.</param>
    /// <param name="id">The id in the setting.</param>
    /// <param name="found"><see langword="true"/> when the id names a preset of the variant, <see langword="false"/> when the default was returned instead.</param>
    /// <returns>The preset.</returns>
    public static ThemePreset Resolve(ThemeBase variant, string id, out bool found)
    {
        ThemePreset? preset = Loaded.Presets.FirstOrDefault(candidate => candidate.Base == variant && string.Equals(candidate.Id, id, StringComparison.Ordinal));
        found = preset is not null;
        return preset ?? Loaded.Presets.First(candidate => candidate.Base == variant && candidate.Id == DefaultId(variant));
    }

    private static string[] Order(ThemeBase variant) =>
    [
        .. Loaded.Presets.Where(preset => preset.Base == variant).Select(preset => preset.Id)
            .OrderBy(id => !string.Equals(id, DefaultId(variant), StringComparison.Ordinal))
            .ThenBy(id => id, StringComparer.Ordinal),
    ];

    private static (IReadOnlyList<ThemePreset>, IReadOnlyList<string>) Load()
    {
        Assembly assembly = typeof(ThemePresets).Assembly;
        List<ThemePreset> presets = [];
        List<string> warnings = [];
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(ResourceSuffix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Missing resource " + name);
            using var reader = new StreamReader(stream);
            ThemeLoadResult result = ThemeFileLoader.Load(reader.ReadToEnd());
            warnings.AddRange(result.Warnings.Select(warning => name + ": " + warning));
            if (result.Preset is not null)
            {
                presets.Add(result.Preset);
            }
        }

        return (presets, warnings);
    }
}
