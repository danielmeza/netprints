using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>Builds <c>schemas/settings.v1.schema.json</c> and the settings table of the guide from the built-in sections (FR-040, research R6).</summary>
internal static class SettingsSchemaFile
{
    /// <summary>The marker that opens the generated table in the guide.</summary>
    public const string Begin = "<!-- settings:begin -->";

    /// <summary>The marker that closes the generated table in the guide.</summary>
    public const string End = "<!-- settings:end -->";

    /// <summary>The canonical <c>$id</c> of the schema.</summary>
    public const string Id = "https://danielmeza.github.io/netprints/schemas/settings.v1.schema.json";

    private const string Header = "| Key | Type | Default | Allowed | Restart | Description |";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Section[] Sections =
    [
        Describe(NetPrintsSettings.Descriptor, NetPrintsSettings.SectionId),
        Describe(RunSettings.Descriptor, RunSettings.SectionId),
        Describe(KeyboardSettings.Descriptor, KeyboardSettings.SectionId),
        Describe(EditorSettings.Descriptor, EditorSettings.SectionId),
    ];

    /// <summary>Gets the canonical text of the schema: two-space indent, <c>\n</c>, final newline.</summary>
    /// <returns>The file content.</returns>
    public static string Generate()
    {
        var extensionProperties = new JsonObject();
        var extensions = new JsonObject { ["type"] = "object", ["properties"] = extensionProperties, ["additionalProperties"] = true };
        var properties = new JsonObject
        {
            ["schemaVersion"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
        };
        foreach (Section section in Sections)
        {
            JsonNode schema = SettingsSchema.Export(section.TypeInfo);
            if (section.Key == EditorSettings.SectionId)
            {
                extensionProperties[section.Key] = schema;
            }
            else
            {
                properties[section.Key] = schema;
            }
        }

        properties["extensions"] = extensions;
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = Id,
            ["title"] = "NetPrints settings (schema v1)",
            ["description"] = "The user settings file of the NetPrints editor; sections and keys this version does not know are preserved",
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = true,
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            root.WriteTo(writer);
        }

        stream.WriteByte((byte)'\n');
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Gets the table of every built-in setting.</summary>
    /// <returns>A Markdown table, without a trailing newline.</returns>
    public static string GenerateTable()
    {
        var lines = new List<string> { Header, "|---|---|---|---|---|---|" };
        foreach (Section section in Sections)
        {
            var schema = (JsonObject)SettingsSchema.Export(section.TypeInfo);
            JsonObject properties = schema["properties"] as JsonObject ?? [];
            foreach (KeyValuePair<string, JsonNode?> property in properties)
            {
                JsonObject node = property.Value as JsonObject ?? [];
                string key = $"`{section.Key}.{property.Key}`";
                string restart = node["x-netprints-restart"] is null ? "" : "Yes";
                string description = (string?)node["description"] ?? "";
                lines.Add($"| {key} | {TypeOf(node)} | {Cell(section.Default[property.Key])} | {Allowed(node)} | {restart} | {Escape(description)} |");
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>Gets the table between the markers of a guide.</summary>
    /// <param name="guide">The guide text, with <c>\n</c> line ends.</param>
    /// <returns>The table.</returns>
    public static string TableOf(string guide)
    {
        int start = guide.IndexOf(Begin, StringComparison.Ordinal) + Begin.Length;
        int end = guide.IndexOf(End, StringComparison.Ordinal);
        return guide[start..end].Trim('\n');
    }

    /// <summary>Replaces the table between the markers of a guide.</summary>
    /// <param name="guide">The guide text.</param>
    /// <param name="table">The new table.</param>
    /// <returns>The guide with the new table.</returns>
    public static string Replace(string guide, string table)
    {
        int start = guide.IndexOf(Begin, StringComparison.Ordinal) + Begin.Length;
        int end = guide.IndexOf(End, StringComparison.Ordinal);
        return string.Concat(guide[..start], "\n\n", table, "\n\n", guide[end..]);
    }

    private static Section Describe<T>(ExtensionSettingsDescriptor<T> descriptor, string key)
    {
        JsonNode? value = JsonSerializer.SerializeToNode(descriptor.Default, descriptor.TypeInfo);
        return new Section(key, descriptor.TypeInfo, value as JsonObject ?? []);
    }

    private static string TypeOf(JsonObject node)
    {
        string type = (string?)node["type"] ?? (node["enum"] is null ? "" : "string");
        if (type == "array" && node["items"] is JsonObject items)
        {
            return $"list of {(string?)items["type"] ?? "value"}";
        }

        return type;
    }

    private static string Allowed(JsonObject node)
    {
        var parts = new List<string>();
        if (node["enum"] is JsonArray values)
        {
            parts.Add(string.Join(", ", values.Select(value => $"`{value}`")));
        }

        if (node["minimum"] is not null && node["maximum"] is not null)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{node["minimum"]} to {node["maximum"]}"));
        }

        if (node["maxLength"] is not null)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"up to {node["maxLength"]} characters"));
        }

        if (node["pattern"] is JsonNode pattern)
        {
            parts.Add($"matches `{Escape(pattern.GetValue<string>())}`");
        }

        if (node["x-netprints-choices"] is JsonNode choices)
        {
            parts.Add($"choices from `{choices.GetValue<string>()}`");
        }

        if (node["x-netprints-path"] is JsonNode path)
        {
            parts.Add($"a {path.GetValue<string>()} path");
        }

        return string.Join("; ", parts);
    }

    private static string Cell(JsonNode? value) => value switch
    {
        null => "",
        JsonArray { Count: 0 } => "empty",
        JsonValue text when text.GetValueKind() == JsonValueKind.String => text.GetValue<string>() is { Length: > 0 } s ? $"`{s}`" : "empty",
        _ => $"`{value.ToJsonString()}`",
    };

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    private sealed record Section(string Key, JsonTypeInfo TypeInfo, JsonObject Default);
}
