using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace NetPrints.Catalog;

/// <summary>
/// Generates the committed JSON Schema for catalog files: the exact text of <c>schemas/npcat.v1.schema.json</c>,
/// built with <see cref="JsonSchemaExporter"/> over <see cref="CatalogDocument"/>.
/// </summary>
public static class CatalogSchema
{
    private const string MetaSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    private const string TypeKeyword = "type";

    private const string StringTypeName = "string";

    private const string PatternKeyword = "pattern";

    private const string PropertiesKeyword = "properties";

    private const string IdPattern = "^[a-z0-9][a-z0-9._-]*$";

    /// <summary>Generates the schema v1 document.</summary>
    /// <returns>The schema text: 2-space indent, <c>\n</c> line endings and one trailing newline.</returns>
    public static string GenerateV1()
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = TransformSchemaNode,
        };

        var schema = (JsonObject)CatalogJsonContext.Default.Options.GetJsonSchemaAsNode(typeof(CatalogDocument), exporterOptions);

        schema.Insert(0, "description", "A NetPrints catalog: the types and members of the covered assemblies, written by 'netprints catalog' (contracts/catalog.md)");
        schema.Insert(0, "title", "NetPrints catalog (schema v1)");
        schema.Insert(0, "$id", CatalogDocument.SchemaUrl);
        schema.Insert(0, "$schema", MetaSchemaUri);

        JsonObject properties = schema[PropertiesKeyword] as JsonObject
            ?? throw new InvalidOperationException("Generated 'CatalogDocument' schema has no 'properties' object.");
        properties.Insert(0, "$schema", new JsonObject { [TypeKeyword] = StringTypeName });

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

        if (schema is not JsonObject obj)
        {
            return schema;
        }

        if (obj[TypeKeyword] is JsonArray types)
        {
            string[] nonNull = [.. types.Select(node => node?.GetValue<string>()).Where(name => name != "null").OfType<string>()];
            obj[TypeKeyword] = nonNull.Length == 1 ? JsonValue.Create(nonNull[0]) : new JsonArray([.. nonNull.Select(name => (JsonNode)JsonValue.Create(name))]);
        }

        if (context.PropertyInfo is { Name: "schemaVersion" })
        {
            obj["const"] = CatalogDocument.CurrentSchemaVersion;
            obj.Remove(TypeKeyword);
        }

        if (context.PropertyInfo is { Name: "id", DeclaringType: var declaringType } && declaringType == typeof(CatalogDocument))
        {
            obj[PatternKeyword] = IdPattern;
        }

        if (context.PropertyInfo is { Name: "assemblies" })
        {
            obj["minItems"] = 1;
        }

        return obj;
    }
}
