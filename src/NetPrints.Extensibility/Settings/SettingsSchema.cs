using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Exports the JSON schema of a settings section, with the NetPrints keywords its metadata attributes add (research R6).
/// </summary>
public static class SettingsSchema
{
    /// <summary>The keyword that marks a property whose change needs a restart.</summary>
    public const string RestartKeyword = "x-netprints-restart";

    /// <summary>The keyword that marks a path property; its value is <c>file</c> or <c>folder</c>.</summary>
    public const string PathKeyword = "x-netprints-path";

    /// <summary>The keyword that names the provider of an open string set.</summary>
    public const string ChoicesKeyword = "x-netprints-choices";

    private const string DescriptionKeyword = "description";

    /// <summary>
    /// Exports the schema of a section.
    /// </summary>
    /// <param name="typeInfo">The section's source-generated JSON metadata.</param>
    /// <returns>The schema, with <c>description</c> from <see cref="DescriptionAttribute"/> and the <c>x-netprints-*</c> keywords on the properties that carry the metadata attributes.</returns>
    public static JsonNode Export(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var options = new JsonSchemaExporterOptions { TransformSchemaNode = Transform };
        return JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, options);
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        ICustomAttributeProvider? provider = context.PropertyInfo?.AttributeProvider;
        if (provider is null || schema is not JsonObject obj)
        {
            return schema;
        }

        if (provider.GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().FirstOrDefault() is { } description)
        {
            obj[DescriptionKeyword] = description.Description;
        }

        if (provider.IsDefined(typeof(RequiresRestartAttribute), true))
        {
            obj[RestartKeyword] = true;
        }

        if (provider.GetCustomAttributes(typeof(SettingPathAttribute), true).OfType<SettingPathAttribute>().FirstOrDefault() is { } path)
        {
            obj[PathKeyword] = path.Kind.ToString().ToLowerInvariant();
        }

        if (provider.GetCustomAttributes(typeof(SettingChoicesAttribute), true).OfType<SettingChoicesAttribute>().FirstOrDefault() is { } choices)
        {
            obj[ChoicesKeyword] = choices.ProviderId;
        }

        return schema;
    }
}
