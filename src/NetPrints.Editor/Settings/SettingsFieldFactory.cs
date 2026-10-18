using System.Text.Json.Nodes;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>Picks the field of a schema property by the table of research R20: the shape of the schema decides, nothing else.</summary>
internal static class SettingsFieldFactory
{
    private const string TypeKeyword = "type";
    private const string EnumKeyword = "enum";
    private const string ItemsKeyword = "items";
    private const string BooleanType = "boolean";
    private const string StringType = "string";
    private const string ArrayType = "array";
    private const string FolderPath = "folder";

    /// <summary>Creates the field of one property.</summary>
    /// <param name="section">The section the property belongs to.</param>
    /// <param name="key">The JSON name of the property.</param>
    /// <param name="schema">The schema of the property.</param>
    /// <param name="services">What the field needs from the host.</param>
    /// <returns>The field, or <see langword="null"/> for a shape no field edits.</returns>
    public static SettingsFieldViewModel? Create(SettingsSection section, string key, JsonObject schema, SettingsFieldServices services)
    {
        if (schema[EnumKeyword] is JsonArray values)
        {
            return new ChoiceFieldViewModel(section, key, schema, [.. values.OfType<JsonValue>().Select(value => value.GetValue<string>())]);
        }

        switch (TypeOf(schema))
        {
            case BooleanType:
                return new ToggleFieldViewModel(section, key, schema);
            case "integer" or "number":
                return new NumberFieldViewModel(section, key, schema);
            case ArrayType when schema[ItemsKeyword] is JsonObject items && TypeOf(items) == StringType:
                return new StringListFieldViewModel(section, key, schema, PathOf(schema), services.Picker);
            case StringType:
                return CreateString(section, key, schema, services);
            default:
                return null;
        }
    }

    /// <summary>Reads the <c>type</c> of a schema node.</summary>
    /// <param name="schema">The schema node.</param>
    /// <returns>The type name, or <see langword="null"/> when the node has none.</returns>
    public static string? TypeOf(JsonObject schema) => schema[TypeKeyword] is JsonValue value && value.TryGetValue(out string? type) ? type : null;

    private static SettingsFieldViewModel CreateString(SettingsSection section, string key, JsonObject schema, SettingsFieldServices services)
    {
        if (schema[SettingsSchema.ChoicesKeyword] is JsonValue provider
            && provider.TryGetValue(out string? providerId)
            && services.Choices(providerId) is { } choices)
        {
            return new ChoiceFieldViewModel(section, key, schema, choices);
        }

        return PathOf(schema) is { } kind
            ? new PathFieldViewModel(section, key, schema, kind, services.Picker)
            : new TextFieldViewModel(section, key, schema);
    }

    private static PathKind? PathOf(JsonObject schema) =>
        schema[SettingsSchema.PathKeyword] is JsonValue value && value.TryGetValue(out string? text)
            ? text == FolderPath ? PathKind.Folder : PathKind.File
            : null;
}
