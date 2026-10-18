using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.State;

/// <summary>The <c>keyboard</c> section of <c>settings.json</c>: the user's differences from the default key bindings (FR-049).</summary>
public sealed record KeyboardSettings
{
    /// <summary>The id of the section; it is a top-level key of the settings file.</summary>
    public const string SectionId = "keyboard";

    /// <summary>Gets the overrides, one entry per changed command.</summary>
    public IReadOnlyList<KeyBindingOverride> Bindings { get; init; } = [];

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<KeyboardSettings> Descriptor { get; } =
        new(SectionId, KeyboardSettingsJsonContext.Default.KeyboardSettings, new KeyboardSettings());
}

/// <summary>The changes the user made to the gestures of one command.</summary>
public sealed record KeyBindingOverride
{
    /// <summary>Gets the id of the command.</summary>
    public string Command { get; init; } = "";

    /// <summary>Gets the name of the command's scope when the entry was written; it is kept for the "Not loaded" list.</summary>
    public string Scope { get; init; } = "Global";

    /// <summary>Gets the gestures the user added, in canonical form.</summary>
    public IReadOnlyList<string> Add { get; init; } = [];

    /// <summary>Gets the default gestures the user removed, in canonical form.</summary>
    public IReadOnlyList<string> Remove { get; init; } = [];
}

/// <summary>Source-generated JSON for <see cref="KeyboardSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(KeyboardSettings))]
internal sealed partial class KeyboardSettingsJsonContext : JsonSerializerContext;
