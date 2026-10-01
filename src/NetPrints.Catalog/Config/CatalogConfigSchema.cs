using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace NetPrints.Catalog;

/// <summary>
/// Generates the committed JSON Schema for configuration files: the exact text of
/// <c>schemas/netprints.catalog.v1.schema.json</c>, built with <see cref="JsonSchemaExporter"/> over <see cref="CatalogConfig"/>.
/// </summary>
internal static class CatalogConfigSchema
{
    private const string MetaSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    private const string TypeKeyword = "type";

    private const string PropertiesKeyword = "properties";

    private const string RequiredKeyword = "required";

    private const string ObjectTypeName = "object";

    private const string StringTypeName = "string";

    private const string MinItemsKeyword = "minItems";

    /// <summary>Generates the schema v1 document.</summary>
    /// <returns>The schema text: 2-space indent, <c>\n</c> line endings and one trailing newline.</returns>
    public static string GenerateV1()
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = TransformSchemaNode,
        };

        var schema = (JsonObject)CatalogConfigJsonContext.Default.Options.GetJsonSchemaAsNode(typeof(CatalogConfig), exporterOptions);

        schema.Insert(0, "description", "The configuration of 'netprints catalog': what to catalog and how (contracts/catalog.md §5)");
        schema.Insert(0, "title", "NetPrints catalog configuration (schema v1)");
        schema.Insert(0, "$id", CatalogConfig.SchemaUrl);
        schema.Insert(0, "$schema", MetaSchemaUri);

        JsonSerializerOptions writeOptions = new()
        {
            WriteIndented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return schema.ToJsonString(writeOptions) + "\n";
    }

    private static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
    {
        Type type = Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type;
        if (type.IsEnum)
        {
            return new JsonObject
            {
                ["enum"] = new JsonArray([.. Enum.GetNames(type).Select(name => (JsonNode)JsonValue.Create(name.ToLowerInvariant()))]),
            };
        }

        if (context.PropertyInfo is { Name: "profile" })
        {
            return ProfileSchema();
        }

        if (schema is not JsonObject obj)
        {
            return schema;
        }

        if (obj[TypeKeyword] is JsonArray types)
        {
            string[] nonNull = [.. types.Select(node => node?.GetValue<string>()).Where(name => name != "null").OfType<string>()];
            obj[TypeKeyword] = nonNull.Length == 1 ? JsonValue.Create(nonNull[0]) : new JsonArray([.. nonNull.Select(name => (JsonNode)JsonValue.Create(name))]);
        }

        switch (context.PropertyInfo)
        {
            case { Name: "schemaVersion" }:
                obj["const"] = CatalogConfig.CurrentSchemaVersion;
                obj.Remove(TypeKeyword);
                break;
            case { Name: "sources" }:
                obj[MinItemsKeyword] = 1;
                break;
            case { Name: "assemblies" }:
                obj[MinItemsKeyword] = 1;
                break;
        }

        if (context.TypeInfo.Type == typeof(CatalogSourceConfig))
        {
            obj["oneOf"] = new JsonArray(
                Requires("assembly"),
                Requires("package", "version"),
                Requires("project", "assemblies"));
        }

        return obj;
    }

    private static JsonObject Requires(params string[] names) =>
        new() { [RequiredKeyword] = new JsonArray([.. names.Select(name => (JsonNode)JsonValue.Create(name))]) };

    private static JsonObject ProfileSchema() => new()
    {
        ["anyOf"] = new JsonArray(
            new JsonObject { [TypeKeyword] = StringTypeName },
            new JsonObject
            {
                [TypeKeyword] = ObjectTypeName,
                ["description"] = "An inline catalog profile with the properties of a '*.npprofile.json' file",
                [RequiredKeyword] = new JsonArray("id"),
                [PropertiesKeyword] = new JsonObject { ["id"] = new JsonObject { [TypeKeyword] = StringTypeName } },
            }),
    };
}
