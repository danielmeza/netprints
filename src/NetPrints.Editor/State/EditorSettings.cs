using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.State;

/// <summary>The editor's own section of <c>settings.json</c>, kept under <c>netprints.editor</c>.</summary>
public sealed record EditorSettings
{
    /// <summary>The id of the section.</summary>
    public const string SectionId = "netprints.editor";

    /// <summary>Gets the theme the editor starts in and View › Theme switches.</summary>
    public EditorTheme Theme { get; init; } = EditorTheme.Dark;

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<EditorSettings> Descriptor { get; } =
        new(SectionId, EditorSettingsJsonContext.Default.EditorSettings, new EditorSettings());
}

/// <summary>Source-generated JSON for <see cref="EditorSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(EditorSettings))]
internal sealed partial class EditorSettingsJsonContext : JsonSerializerContext;
