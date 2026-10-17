using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.State;

/// <summary>The editor's own section of <c>settings.json</c>, kept under <c>netprints.editor</c>.</summary>
public sealed record EditorSettings
{
    /// <summary>The id of the section.</summary>
    public const string SectionId = "netprints.editor";

    /// <summary>The id of the choices provider that lists the presets of the Dark variant.</summary>
    public const string DarkPresetChoices = "netprints.presets.dark";

    /// <summary>The id of the choices provider that lists the presets of the Light variant.</summary>
    public const string LightPresetChoices = "netprints.presets.light";

    private const int MaxPresetIdLength = 128;
    private const string PresetIdPattern = "^[a-z0-9]+([._-][a-z0-9]+)*$";

    /// <summary>The id of the Dark variant's default preset.</summary>
    public const string DefaultDarkPreset = "netprints.dark";

    /// <summary>The id of the Light variant's default preset.</summary>
    public const string DefaultLightPreset = "netprints.light";

    /// <summary>Gets the theme the editor starts in and View › Theme switches.</summary>
    [EnumDataType(typeof(EditorTheme))]
    [Description("The theme variant the editor starts in")]
    public EditorTheme Theme { get; init; } = EditorTheme.Dark;

    /// <summary>Gets the id of the preset the Dark variant uses.</summary>
    [StringLength(MaxPresetIdLength, MinimumLength = 1)]
    [RegularExpression(PresetIdPattern)]
    [SettingChoices(DarkPresetChoices)]
    [Description("The id of the preset the Dark variant uses")]
    public string DarkPreset { get; init; } = DefaultDarkPreset;

    /// <summary>Gets the id of the preset the Light variant uses.</summary>
    [StringLength(MaxPresetIdLength, MinimumLength = 1)]
    [RegularExpression(PresetIdPattern)]
    [SettingChoices(LightPresetChoices)]
    [Description("The id of the preset the Light variant uses")]
    public string LightPreset { get; init; } = DefaultLightPreset;

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<EditorSettings> Descriptor { get; } =
        new(SectionId, EditorSettingsJsonContext.Default.EditorSettings, new EditorSettings())
        {
            Validator = Validate,
        };

    private static string[] Validate(EditorSettings value)
        => [.. new EditorSettingsValidator().Validate(null, value).Failures ?? []];
}

/// <summary>Validates <see cref="EditorSettings"/> through the DataAnnotations on its members (generated).</summary>
[OptionsValidator]
internal sealed partial class EditorSettingsValidator : IValidateOptions<EditorSettings>;

/// <summary>Source-generated JSON for <see cref="EditorSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(EditorSettings))]
internal sealed partial class EditorSettingsJsonContext : JsonSerializerContext;
