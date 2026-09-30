#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Migrations;

namespace NetPrints.Serialization.Json;

/// <summary>
/// Reads and writes class graph documents in NetPrints' canonical JSON form (document-format.md §2.2):
/// on read, a leading <c>$schema</c> property is stripped and the document is migrated to the current
/// schema version before being deserialized; on write, <c>$schema</c> is inserted first and the result
/// is written through <see cref="CanonicalJsonWriter"/>, with every graph's <c>connections</c> sorted by
/// ordinal <c>from</c>, then <c>to</c> whatever order they were read in (the editor and <c>netprints format</c> share that one order).
/// </summary>
public sealed class JsonDocumentFormat : IDocumentFormat
{
    private const string ConnectionsProperty = "connections";
    private const string FromProperty = "from";
    private const string ToProperty = "to";

    private readonly NetPrintsJsonOptions options;
    private readonly DocumentMigrator migrator;

    /// <summary>
    /// Creates a JSON document format.
    /// </summary>
    /// <param name="options">Serializer options for <see cref="Documents.ClassDocument"/> and its node
    /// documents.</param>
    /// <param name="migrator">Migrator used to upgrade older schema versions before deserializing.</param>
    public JsonDocumentFormat(NetPrintsJsonOptions options, DocumentMigrator migrator)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
    }

    /// <inheritdoc/>
    public string Id => "json";

    /// <inheritdoc/>
    public IReadOnlyList<string> ClassExtensions { get; } = [".netpc.json"];

    /// <inheritdoc/>
    public bool CanWrite => true;

    /// <inheritdoc/>
    /// <remarks>
    /// The root must be a JSON object. A <c>$schema</c> property, if present, must be a string (its
    /// value is not otherwise checked) and is removed before migrating and deserializing. Syntax
    /// errors carry <see cref="DocumentFormatException.Line"/> and
    /// <see cref="DocumentFormatException.BytePosition"/>; errors found after parsing carry the JSON
    /// path (<see cref="JsonException.Path"/>) in the exception message instead.
    /// </remarks>
    public async ValueTask<ClassDocument> ReadClassAsync(Stream input, DocumentId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        JsonNode? root;
        try
        {
            root = await JsonNode.ParseAsync(input, nodeOptions: null, NetPrintsJsonOptions.DocumentOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            long? line = ex.LineNumber is { } lineNumber ? lineNumber + 1 : null;
            throw new DocumentFormatException(ex.Message, id, line, ex.BytePositionInLine, ex);
        }

        if (root is not JsonObject rootObject)
        {
            throw new DocumentFormatException("The document root must be a JSON object.", id);
        }

        if (rootObject.TryGetPropertyValue("$schema", out JsonNode? schemaNode))
        {
            if (schemaNode is not JsonValue schemaValue || !schemaValue.TryGetValue(out string? _))
            {
                throw new DocumentFormatException("'$schema' must be a string.", id);
            }

            rootObject.Remove("$schema");
        }

        JsonObject migrated = migrator.Upgrade(rootObject, DocumentKind.Class, id);

        try
        {
            return JsonSerializer.Deserialize(migrated, ClassTypeInfo())
                ?? throw new DocumentFormatException("The document deserialized to null.", id);
        }
        catch (JsonException ex)
        {
            throw new DocumentFormatException(ex.Message, id, inner: ex);
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteClassAsync(ClassDocument document, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();

        JsonNode? serializedNode = JsonSerializer.SerializeToNode(document, ClassTypeInfo());
        if (serializedNode is not JsonObject serialized)
        {
            throw new InvalidOperationException($"'{nameof(ClassDocument)}' did not serialize to a JSON object.");
        }

        var root = new JsonObject { ["$schema"] = NetPrintsSchema.V1Url };

        foreach ((string key, JsonNode? value) in serialized.ToList())
        {
            serialized.Remove(key);
            root[key] = value;
        }

        SortConnections(root);
        CanonicalJsonWriter.Write(root, output);
        return ValueTask.CompletedTask;
    }

    private static void SortConnections(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string key, JsonNode? value) in obj)
                {
                    if (key == ConnectionsProperty && value is JsonArray connections)
                    {
                        SortArray(connections);
                    }
                    else
                    {
                        SortConnections(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    SortConnections(item);
                }

                break;
        }
    }

    private static void SortArray(JsonArray connections)
    {
        JsonNode?[] sorted = [.. connections
            .OrderBy(connection => EndpointOf(connection, FromProperty), StringComparer.Ordinal)
            .ThenBy(connection => EndpointOf(connection, ToProperty), StringComparer.Ordinal)];
        connections.Clear();
        foreach (JsonNode? connection in sorted)
        {
            connections.Add(connection);
        }
    }

    private static string EndpointOf(JsonNode? connection, string property) =>
        connection is JsonObject obj && obj.TryGetPropertyValue(property, out JsonNode? value) && value is JsonValue text && text.TryGetValue(out string? endpoint)
            ? endpoint
            : string.Empty;

    private JsonTypeInfo<ClassDocument> ClassTypeInfo() =>
        options.SerializerOptions.GetTypeInfo(typeof(ClassDocument)) as JsonTypeInfo<ClassDocument>
        ?? throw new InvalidOperationException($"No serializer metadata is registered for '{nameof(ClassDocument)}'.");
}
