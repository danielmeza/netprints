using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NetPrints.Catalog;

/// <summary>
/// Reads catalog files (<c>*.npcat.json</c>). Tolerant of unknown properties and any whitespace; a newer
/// <c>schemaVersion</c> fails with NPC101 and any other defect with NPC102.
/// </summary>
public static class CatalogReader
{
    private const string SchemaVersionProperty = "schemaVersion";

    /// <summary>Reads a catalog from a UTF-8 stream.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="CatalogFormatException">The file is malformed or its schema version is newer than supported.</exception>
    public static CatalogDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using JsonDocument json = JsonDocument.Parse(stream);
            return Convert(json);
        }
        catch (JsonException exception)
        {
            throw Malformed(exception.Message, exception);
        }
    }

    /// <summary>Reads a catalog from JSON text.</summary>
    /// <param name="json">The catalog text.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="CatalogFormatException">The file is malformed or its schema version is newer than supported.</exception>
    public static CatalogDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Convert(document);
        }
        catch (JsonException exception)
        {
            throw Malformed(exception.Message, exception);
        }
    }

    private static CatalogDocument Convert(JsonDocument json)
    {
        JsonElement root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("the root is not an object");
        }

        if (!root.TryGetProperty(SchemaVersionProperty, out JsonElement version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schemaVersion))
        {
            throw Malformed($"'{SchemaVersionProperty}' is missing or not an integer");
        }

        if (schemaVersion > CatalogDocument.CurrentSchemaVersion)
        {
            throw new CatalogFormatException(
                CatalogDiagnosticCodes.UnsupportedSchemaVersion,
                $"Catalog schemaVersion {schemaVersion} is not supported; this reader supports up to {CatalogDocument.CurrentSchemaVersion}.");
        }

        CatalogDocument document = root.Deserialize(CatalogJsonContext.Default.CatalogDocument)
            ?? throw Malformed("the document is empty");
        document = document with { Types = document.Types ?? [] };
        Validate(document);
        return document;
    }

    private static void Validate(CatalogDocument document)
    {
        if (document.Assemblies.Count == 0)
        {
            throw Malformed("'assemblies' must list at least one assembly");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CatalogType type in document.Types)
        {
            if (!ids.Add(type.Id))
            {
                throw Malformed($"the type id '{type.Id}' appears more than once");
            }
        }
    }

    private static CatalogFormatException Malformed(string detail, Exception? inner = null) =>
        new(CatalogDiagnosticCodes.MalformedCatalog, $"The catalog file is malformed: {detail}.", inner);
}
