using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.State;

/// <summary>The <c>keyboard</c> section of <c>settings.json</c>: the user's differences from the default key bindings (FR-049).</summary>
public sealed record KeyboardSettings
{
    /// <summary>The id of the section; it is a top-level key of the settings file.</summary>
    public const string SectionId = "keyboard";

    /// <summary>Gets the overrides, one entry per changed command; a <see langword="null"/> entry in the file is not an entry and is dropped, an entry without a command is kept and listed as not loaded.</summary>
    [Description("Your changes to the default key bindings, one entry per changed command")]
    [JsonConverter(typeof(KeyBindingListConverter))]
    public IReadOnlyList<KeyBindingOverride> Bindings { get; init; } = [];

    /// <summary>Gets the descriptor that reads and writes the section through the settings store.</summary>
    public static ExtensionSettingsDescriptor<KeyboardSettings> Descriptor { get; } =
        new(SectionId, KeyboardSettingsJsonContext.Default.KeyboardSettings, new KeyboardSettings());
}

/// <summary>
/// Reads the <c>bindings</c> array entry by entry, so a bad entry costs only itself: a <see langword="null"/> element is dropped, an
/// entry that cannot be read becomes an entry without a command that keeps its raw JSON and the reason (ADR-0040 decision 5).
/// </summary>
internal sealed class KeyBindingListConverter : JsonConverter<IReadOnlyList<KeyBindingOverride>>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override IReadOnlyList<KeyBindingOverride> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("keyboard.bindings must be an array.");
        }

        List<KeyBindingOverride> entries = [];
        int index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            JsonElement element = JsonElement.ParseValue(ref reader);
            if (element.ValueKind != JsonValueKind.Null)
            {
                entries.Add(ReadEntry(element, index));
            }

            index++;
        }

        return entries;
    }

    private static KeyBindingOverride ReadEntry(JsonElement element, int index)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(Strings.Settings_Keyboard_EntryNotObject);
            }

            return element.Deserialize(KeyboardSettingsJsonContext.Default.KeyBindingOverride) ?? throw new JsonException(Strings.Settings_Keyboard_EntryNotObject);
        }
        catch (JsonException ex)
        {
            string command = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("command", out JsonElement named) && named.ValueKind == JsonValueKind.String
                ? TextFormat.Of(Strings.Settings_Keyboard_EntryCommand, named.GetString())
                : "";
            return new KeyBindingOverride
            {
                RawJson = element.GetRawText(),
                Problem = TextFormat.Of(Strings.Settings_Keyboard_EntryProblem, index, command, ex.Message),
                ProblemSummary = TextFormat.Of(Strings.Settings_Keyboard_EntryProblemSummary, command, ex.Message),
            };
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<KeyBindingOverride> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (KeyBindingOverride entry in value)
        {
            if (entry.RawJson is { } raw)
            {
                writer.WriteRawValue(raw);
            }
            else
            {
                JsonSerializer.Serialize(writer, entry, KeyboardSettingsJsonContext.Default.KeyBindingOverride);
            }
        }

        writer.WriteEndArray();
    }
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
    public KeyBindingOverride(string? command = null, string? scope = DefaultScope, IReadOnlyList<string>? add = null, IReadOnlyList<string>? remove = null)
    {
        Command = command ?? "";
        Scope = scope ?? DefaultScope;
        Add = add ?? [];
        Remove = remove ?? [];
    }

    /// <summary>Gets the id of the command; empty for an entry the file wrote without one, which is kept and listed as not loaded.</summary>
    [Description("The id of the command")]
    [Required(AllowEmptyStrings = false)]
    public string Command { get; init; }

    /// <summary>Gets the raw JSON of an entry that could not be read; it is written back unchanged. <see langword="null"/> for a readable entry.</summary>
    [JsonIgnore]
    public string? RawJson { get; init; }

    /// <summary>Gets why the entry could not be read, naming its index and command; <see langword="null"/> for a readable entry.</summary>
    [JsonIgnore]
    public string? Problem { get; init; }

    /// <summary>Gets why the entry could not be read, naming its command but not its position, for the "Not loaded" row; <see langword="null"/> for a readable entry.</summary>
    [JsonIgnore]
    public string? ProblemSummary { get; init; }

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
[JsonSerializable(typeof(KeyBindingOverride))]
internal sealed partial class KeyboardSettingsJsonContext : JsonSerializerContext;
