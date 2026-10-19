using System.ComponentModel;
using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.State;

/// <summary>The <c>keyboard</c> section of <c>settings.json</c>: the user's differences from the default key bindings (FR-049).</summary>
public sealed record KeyboardSettings
{
    private const string NullEntryReason = "keyboard.bindings: an entry is null";
    private const string MissingCommandReason = "keyboard.bindings: an entry has no command";

    /// <summary>The id of the section; it is a top-level key of the settings file.</summary>
    public const string SectionId = "keyboard";

    /// <summary>Gets the overrides, one entry per changed command.</summary>
    [Description("Your changes to the default key bindings, one entry per changed command")]
    public IReadOnlyList<KeyBindingOverride> Bindings { get; init; } = [];

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<KeyboardSettings> Descriptor { get; } =
        new(SectionId, KeyboardSettingsJsonContext.Default.KeyboardSettings, new KeyboardSettings())
        {
            Validator = Validate,
        };

    private static List<string> Validate(KeyboardSettings settings) =>
        settings.Bindings.Any(entry => entry is null) ? [NullEntryReason]
        : settings.Bindings.Any(entry => entry.Command.Length == 0) ? [MissingCommandReason]
        : [];
}

/// <summary>The changes the user made to the gestures of one command.</summary>
public sealed record KeyBindingOverride
{
    private const string DefaultScope = "Global";

    /// <summary>Creates an entry; a member the file omits or writes as <see langword="null"/> takes its default.</summary>
    /// <param name="command">The id of the command; empty when omitted.</param>
    /// <param name="scope">The name of the command's scope; <c>Global</c> when omitted.</param>
    /// <param name="add">The gestures the user added; none when omitted.</param>
    /// <param name="remove">The default gestures the user removed; none when omitted.</param>
    [JsonConstructor]
    public KeyBindingOverride(string? command = null, string? scope = null, IReadOnlyList<string>? add = null, IReadOnlyList<string>? remove = null)
    {
        Command = command ?? "";
        Scope = scope ?? DefaultScope;
        Add = add ?? [];
        Remove = remove ?? [];
    }

    /// <summary>Gets the id of the command.</summary>
    [Description("The id of the command")]
    public string Command { get; init; }

    /// <summary>Gets the name of the command's scope when the entry was written; it is kept for the "Not loaded" list.</summary>
    [Description("The scope of the command when the entry was written")]
    public string Scope { get; init; }

    /// <summary>Gets the gestures the user added, in canonical form.</summary>
    [Description("Gestures added to the command, in canonical form")]
    public IReadOnlyList<string> Add { get; init; }

    /// <summary>Gets the default gestures the user removed, in canonical form.</summary>
    [Description("Default gestures removed from the command, in canonical form")]
    public IReadOnlyList<string> Remove { get; init; }
}

/// <summary>Source-generated JSON for <see cref="KeyboardSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(KeyboardSettings))]
internal sealed partial class KeyboardSettingsJsonContext : JsonSerializerContext;
