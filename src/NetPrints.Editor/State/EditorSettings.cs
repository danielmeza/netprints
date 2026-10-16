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

    /// <summary>Gets the theme the editor starts in and View › Theme switches.</summary>
    [EnumDataType(typeof(EditorTheme))]
    public EditorTheme Theme { get; init; } = EditorTheme.Dark;

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<EditorSettings> Descriptor { get; } =
        new(SectionId, EditorSettingsJsonContext.Default.EditorSettings, new EditorSettings())
        {
            Validator = new EditorSettingsValidator().Validate,
        };
}

/// <summary>Validates <see cref="EditorSettings"/> through the DataAnnotations on its members (generated).</summary>
[OptionsValidator]
internal sealed partial class EditorSettingsValidator : IValidateOptions<EditorSettings>;

/// <summary>Source-generated JSON for <see cref="EditorSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(EditorSettings))]
internal sealed partial class EditorSettingsJsonContext : JsonSerializerContext;
