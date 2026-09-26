#nullable enable
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;

namespace NetPrints.Serialization.Json;

/// <summary>
/// The <see cref="NetPrintsSchema.V1Url"/> JSON Schema's URL constant (document-format.md §6), used as
/// the first property of every written graph document.
/// </summary>
public static class NetPrintsSchema
{
    /// <summary>URL of the published v1 JSON Schema.</summary>
    public const string V1Url = "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json";
}

/// <summary>
/// Builds the frozen <see cref="JsonSerializerOptions"/> every graph document is read and written with
/// (document-format.md §2.3): <see cref="NetPrintsJsonContext"/>'s source-generated metadata for every
/// built-in type, combined with each loaded extension's own resolver, plus a modifier that registers
/// every extension node kind as a polymorphic derived type of <see cref="NodeDocument"/>.
/// </summary>
public sealed class NetPrintsJsonOptions
{
    /// <summary>
    /// Builds the options for <paramref name="nodes"/>'s registered converters and extension
    /// resolvers.
    /// </summary>
    /// <param name="nodes">The node document converters and extension resolvers to support.</param>
    public NetPrintsJsonOptions(NodeDocumentConverterRegistry nodes)
    {
        IJsonTypeInfoResolver resolver = JsonTypeInfoResolver
            .Combine([NetPrintsJsonContext.Default, .. nodes.ExtensionResolvers])
            .WithAddedModifier(typeInfo => AddExtensionKinds(typeInfo, nodes));

        SerializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            // Verified empirically (implementation-notes.md, T026): a source-generated JsonTypeInfo
            // resolved through a *different* JsonSerializerOptions instance than the one the context
            // built its own Options from (exactly what combining with extension resolvers requires)
            // only keeps its per-property converters and ignore conditions; naming policy and the
            // blanket default-ignore condition are re-derived from the resolving options, so both are
            // repeated here even though NetPrintsJsonContext already declares them.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            AllowOutOfOrderMetadataProperties = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        SerializerOptions.MakeReadOnly();
    }

    /// <summary>
    /// The frozen serializer options built from <see cref="NetPrintsJsonContext"/> and every loaded
    /// extension's resolver.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Options for parsing a document's raw <see cref="System.Text.Json.Nodes.JsonNode"/> tree before
    /// mapping it (document-format.md §2.2): comments skipped, trailing commas allowed.
    /// </summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static void AddExtensionKinds(JsonTypeInfo typeInfo, NodeDocumentConverterRegistry nodes)
    {
        if (typeInfo.Type != typeof(NodeDocument))
        {
            return;
        }

        JsonPolymorphismOptions polymorphism = typeInfo.PolymorphismOptions
            ?? throw new InvalidOperationException($"'{nameof(NodeDocument)}' has no polymorphism options.");

        foreach (INodeDocumentConverter converter in nodes.Converters)
        {
            if (converter.Kind.Contains('/'))
            {
                polymorphism.DerivedTypes.Add(new JsonDerivedType(converter.DocumentType, converter.Kind));
            }
        }
    }
}
