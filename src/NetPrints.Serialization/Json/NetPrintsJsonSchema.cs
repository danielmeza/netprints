#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Core;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Json;

/// <summary>
/// Generates the committed JSON Schema for the graph document format (document-format.md §6): the
/// exact text of <c>schemas/netpc.v1.schema.json</c>, built with <see cref="JsonSchemaExporter"/> over
/// <see cref="ClassDocument"/> and <see cref="NetPrintsJsonContext"/>'s built-in contract only (loaded
/// extensions never change this file).
/// </summary>
public static class NetPrintsJsonSchema
{
    /// <summary>Draft the schema declares itself against.</summary>
    private const string MetaSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>Document types whose <c>id</c> property is a member id (document-format.md §1.4).</summary>
    private static readonly Type[] MemberDocumentTypes =
        [typeof(VariableDocument), typeof(MethodDocument), typeof(ConstructorDocument), typeof(EventGraphDocument)];

    /// <summary>
    /// <see cref="ConnectionDocument.From"/>/<see cref="ConnectionDocument.To"/>'s pattern
    /// (document-format.md §6): a node id followed by a pin reference (document-format.md §1.4.2),
    /// built from <see cref="IdFormat.PatternFor"/>'s node pattern so the id shape is never typed
    /// twice.
    /// </summary>
    private static readonly string ConnectionEndpointPattern =
        $"{StripTrailingAnchor(IdFormat.PatternFor('n'))}/(in|out)\\.(exec|data|type)\\..+$";

    /// <summary>
    /// The root <c>layout</c> object's graph-key pattern (document-format.md §1.4.1, §6): <c>class</c>,
    /// a member id, or a member id with <c>/type</c>, <c>/get</c> or <c>/set</c>; built from
    /// <see cref="IdFormat.PatternFor"/>'s member pattern.
    /// </summary>
    private static readonly string LayoutGraphKeyPattern =
        $"^(class|{StripAnchors(IdFormat.PatternFor('m'))}(/(type|get|set))?)$";

    /// <summary>Removes the leading <c>^</c> and trailing <c>$</c> anchors so a pattern's body can be
    /// embedded inside a larger one.</summary>
    private static string StripAnchors(string anchoredPattern) => anchoredPattern[1..^1];

    /// <summary>Removes only the trailing <c>$</c> anchor, so a pattern can be extended with more text
    /// before a new one is added at the end.</summary>
    private static string StripTrailingAnchor(string anchoredPattern) => anchoredPattern[..^1];

    /// <summary>
    /// Generates the schema v1 document: <see cref="JsonSchemaExporter"/> over <see cref="ClassDocument"/>,
    /// with root metadata added, <c>$schema</c> declared as a string property, <c>schemaVersion</c>
    /// pinned to <c>1</c>, layout positions constrained to 2-element integer arrays, an extra
    /// polymorphism branch on <see cref="NodeDocument"/> for extension node kinds, and every object's
    /// <c>required</c> list corrected to the fields document-format.md §1.4–§1.6 marks Req. = yes
    /// (research.md R17, open item K14).
    /// </summary>
    /// <returns>The schema text, ready to write byte-identically to <c>schemas/netpc.v1.schema.json</c>
    /// (2-space indent, <c>\n</c> line endings, one trailing newline).</returns>
    public static string GenerateV1()
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = TransformSchemaNode,
        };

        var schema = (JsonObject)NetPrintsJsonContext.Default.Options.GetJsonSchemaAsNode(typeof(ClassDocument), exporterOptions);

        // Root metadata (document-format.md §6): $schema/$id/title first, ahead of the exporter's own
        // "type"/"properties"/"required" keys.
        schema.Insert(0, "title", "NetPrints class graph (schema v1)");
        schema.Insert(0, "$id", NetPrintsSchema.V1Url);
        schema.Insert(0, "$schema", MetaSchemaUri);

        // The writer's own `$schema` header property (document-format.md §1.1) is not a member of
        // ClassDocument (JsonDocumentFormat strips it on read and adds it on write around plain
        // serialization), so the exporter never sees it; add it to the object schema by hand.
        JsonObject properties = schema["properties"] as JsonObject
            ?? throw new InvalidOperationException("Generated 'ClassDocument' schema has no 'properties' object.");
        properties.Insert(0, "$schema", new JsonObject { ["type"] = "string" });

        JsonSerializerOptions writeOptions = new()
        {
            WriteIndented = true,
            IndentSize = 2,
            NewLine = "\n",
            // Relaxed escaping (matching the canonical document writer, document-format.md §2.3): the
            // default encoder escapes '+' (the connection-endpoint pattern's "one or more") as +.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return schema.ToJsonString(writeOptions) + "\n";
    }

    private static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is not JsonObject obj)
        {
            return schema;
        }

        if (context.TypeInfo.Type == typeof(int[]))
        {
            obj["minItems"] = 2;
            obj["maxItems"] = 2;
        }

        if (context.PropertyInfo is { Name: "schemaVersion" })
        {
            obj["const"] = 1;
        }

        if (context.TypeInfo.Type == typeof(NodeDocument) && obj["anyOf"] is JsonArray anyOf)
        {
            anyOf.Add(new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("$kind", "id"),
                ["properties"] = new JsonObject
                {
                    ["$kind"] = new JsonObject { ["type"] = "string", ["pattern"] = "/" },
                    ["id"] = new JsonObject { ["type"] = "string", ["pattern"] = IdFormat.PatternFor('n') },
                },
            });
        }

        // Id patterns (research.md R21, T054b): every node/member id and connection endpoint, plus the
        // layout maps' own keys, built from IdFormat so the shape is never typed twice.
        if (context.PropertyInfo is { Name: "id" } idProperty)
        {
            if (idProperty.DeclaringType == typeof(NodeDocument))
            {
                obj["pattern"] = IdFormat.PatternFor('n');
            }
            else if (Array.IndexOf(MemberDocumentTypes, idProperty.DeclaringType) >= 0)
            {
                obj["pattern"] = IdFormat.PatternFor('m');
            }
        }
        else if (context.PropertyInfo is { Name: "from" or "to", DeclaringType: var declaringType } && declaringType == typeof(ConnectionDocument))
        {
            obj["pattern"] = ConnectionEndpointPattern;
        }
        else if (context.TypeInfo.Type == typeof(SortedDictionary<string, SortedDictionary<string, int[]>>))
        {
            obj["propertyNames"] = new JsonObject { ["pattern"] = LayoutGraphKeyPattern };
        }
        else if (context.TypeInfo.Type == typeof(SortedDictionary<string, int[]>))
        {
            obj["propertyNames"] = new JsonObject { ["pattern"] = IdFormat.PatternFor('n') };
        }

        FixRequired(context, obj);

        return obj;
    }

    /// <summary>
    /// Recomputes an object schema's <c>required</c> array (document-format.md §6): the exporter marks
    /// every constructor parameter without a C# default value as required, regardless of nullability or
    /// of <see cref="JsonIgnoreCondition.WhenWritingDefault"/> (research.md R17, K14). Two corrections:
    /// a type that marks at least one member <see cref="JsonIgnoreCondition.Never"/> (the convention
    /// every <c>ClassDocument</c>/member/node-common field uses for Req. = yes, document-format.md §1.1)
    /// is required exactly on those members and no others; a type with no such member (the reference
    /// and value DTOs of §1.6, which rely on nullability and C# default parameter values instead) keeps
    /// the exporter's own list minus any member the exporter over-included despite being nullable.
    /// </summary>
    private static void FixRequired(JsonSchemaExporterContext context, JsonObject obj)
    {
        if (obj["properties"] is not JsonObject properties)
        {
            return;
        }

        JsonPropertyInfo? Find(string jsonName) => context.TypeInfo.Properties.FirstOrDefault(p => p.Name == jsonName);

        bool usesNeverConvention = context.TypeInfo.Properties.Any(IsAlwaysWritten);
        var wasRequired = (obj["required"] as JsonArray)?.Select(n => n?.GetValue<string>()).ToHashSet() ?? [];

        var required = new JsonArray();
        foreach (string name in properties.Select(p => p.Key))
        {
            JsonPropertyInfo? property = Find(name);
            bool isRequired = property is not null && (usesNeverConvention
                ? IsAlwaysWritten(property)
                : wasRequired.Contains(name) && !IsNullableProperty(property));

            if (isRequired)
            {
                required.Add(name);
            }
        }

        if (required.Count > 0)
        {
            obj["required"] = required;
        }
        else
        {
            obj.Remove("required");
        }
    }

    private static bool IsAlwaysWritten(JsonPropertyInfo property) =>
        property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true)
            .OfType<JsonIgnoreAttribute>()
            .Any(a => a.Condition == JsonIgnoreCondition.Never) == true;

    /// <summary>
    /// Whether <paramref name="property"/> is nullable: a <see cref="Nullable{T}"/> value type, or a
    /// reference type whose nullable annotation (read from the underlying <see cref="PropertyInfo"/>,
    /// not from the already-generated child schema node, which may be a <c>$ref</c> to an earlier
    /// occurrence of the same shape and so carry no <c>type</c> keyword of its own) is not
    /// <see cref="NullabilityState.NotNull"/>.
    /// </summary>
    private static bool IsNullableProperty(JsonPropertyInfo property)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            return true;
        }

        if (property.PropertyType.IsValueType)
        {
            return false;
        }

        return property.AttributeProvider is PropertyInfo reflected
            && new NullabilityInfoContext().Create(reflected).ReadState != NullabilityState.NotNull;
    }
}
