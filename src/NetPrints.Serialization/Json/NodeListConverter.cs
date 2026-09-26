#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Json;

/// <summary>
/// Converts a graph's node list (document-format.md §2.3, research R5): reads each element as a
/// buffered <see cref="JsonElement"/>, finds <c>$kind</c> and <c>id</c> wherever they appear in the
/// object, and dispatches to the matching <see cref="NodeDocument"/> subtype registered on
/// <see cref="NodeDocument"/>'s <see cref="JsonPolymorphismOptions"/> (built-in, or an extension kind
/// added at runtime by <see cref="NetPrintsJsonOptions"/>) — falling back to
/// <see cref="UnknownNodeDocument"/>, with the element's full original content preserved, for any
/// <c>$kind</c> this build does not know. Writing re-emits an <see cref="UnknownNodeDocument"/>'s
/// original content (<c>$kind</c> and <c>id</c> first, its other properties in source order) and
/// serializes every other node through <see cref="NodeDocument"/>'s normal polymorphic dispatch.
/// </summary>
internal sealed class NodeListConverter : JsonConverter<IReadOnlyList<NodeDocument>>
{
    /// <summary>
    /// Reads a JSON array of nodes.
    /// </summary>
    /// <param name="reader">Reader positioned at the array's start token.</param>
    /// <param name="typeToConvert">Unused.</param>
    /// <param name="options">Serializer options; used to resolve <see cref="NodeDocument"/>'s
    /// registered derived types and to deserialize each recognized element.</param>
    /// <returns>The read nodes, in array order.</returns>
    /// <exception cref="JsonException">The value is not a JSON array.</exception>
    /// <exception cref="DocumentFormatException">An element is missing <c>$kind</c> or <c>id</c>, or
    /// is not a JSON object.</exception>
    public override IReadOnlyList<NodeDocument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected a JSON array for a graph's node list.");
        }

        IList<JsonDerivedType> derivedTypes = options.GetTypeInfo(typeof(NodeDocument)).PolymorphismOptions?.DerivedTypes
            ?? throw new InvalidOperationException($"'{nameof(NodeDocument)}' has no polymorphism options.");

        var nodes = new List<NodeDocument>();
        reader.Read();

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            // ParseValue leaves the reader positioned on the parsed value's own last token; Read()
            // advances to the next array element (or EndArray) before the loop condition is checked.
            JsonElement element = JsonElement.ParseValue(ref reader);
            nodes.Add(ReadOne(element, derivedTypes, options));
            reader.Read();
        }

        return nodes;
    }

    /// <summary>
    /// Writes a JSON array of nodes.
    /// </summary>
    /// <param name="writer">Writer to write the array to.</param>
    /// <param name="value">Nodes to write, in array order.</param>
    /// <param name="options">Serializer options used to serialize every recognized node.</param>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<NodeDocument> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (NodeDocument node in value)
        {
            if (node is UnknownNodeDocument unknown)
            {
                WriteUnknown(writer, unknown);
            }
            else
            {
                // Declared type NodeDocument, not the node's runtime type, so the polymorphic
                // ($kind) dispatch registered on NodeDocument fires.
                JsonSerializer.Serialize(writer, node, typeof(NodeDocument), options);
            }
        }

        writer.WriteEndArray();
    }

    private static NodeDocument ReadOne(JsonElement element, IList<JsonDerivedType> derivedTypes, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DocumentFormatException("A node must be a JSON object.");
        }

        string? kind = null;
        string? id = null;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals("$kind"))
            {
                kind = property.Value.GetString();
            }
            else if (property.NameEquals("id"))
            {
                id = property.Value.GetString();
            }
        }

        if (kind is null || id is null)
        {
            throw new DocumentFormatException("A node is missing '$kind' or 'id'.");
        }

        foreach (JsonDerivedType derivedType in derivedTypes)
        {
            if (derivedType.TypeDiscriminator is string discriminator && discriminator == kind)
            {
                return (NodeDocument?)element.Deserialize(derivedType.DerivedType, options)
                    ?? throw new DocumentFormatException($"Node '{id}' deserialized to null.");
            }
        }

        return new UnknownNodeDocument(id, kind, element);
    }

    private static void WriteUnknown(Utf8JsonWriter writer, UnknownNodeDocument unknown)
    {
        writer.WriteStartObject();
        writer.WriteString("$kind", unknown.Kind);
        writer.WriteString("id", unknown.Id);

        foreach (JsonProperty property in unknown.Raw.EnumerateObject())
        {
            if (property.NameEquals("$kind") || property.NameEquals("id"))
            {
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
