#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace NetPrints.Serialization.Migrations;

/// <summary>
/// Upgrades a document's <c>schemaVersion</c> to the version this build supports, by chaining
/// registered <see cref="IDocumentMigration"/>s (document-format.md §2.5). P1 ships no migration
/// (schema v1 is the first JSON schema); <see cref="Supported"/> still reflects whatever migrations
/// are actually registered, so a test can register a synthetic migration without bumping
/// <see cref="CurrentSchemaVersion"/> (DF-T10).
/// </summary>
public sealed class DocumentMigrator
{
    /// <summary>
    /// The current schema version graph documents are written in, when no migration extends it
    /// further (<c>1</c> for P1; see <see cref="Supported"/>).
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private readonly Dictionary<(DocumentKind Kind, int FromVersion), IDocumentMigration> migrationsByKindAndVersion;

    /// <summary>
    /// Creates a migrator from an explicit list of migrations.
    /// </summary>
    /// <param name="migrations">Every registered migration.</param>
    /// <exception cref="ArgumentException">Two migrations share a <see cref="IDocumentMigration.Kind"/>
    /// and <see cref="IDocumentMigration.FromVersion"/>, or a kind's migrations do not form a
    /// contiguous chain starting at version 1.</exception>
    public DocumentMigrator(IReadOnlyList<IDocumentMigration> migrations)
    {
        migrationsByKindAndVersion = new Dictionary<(DocumentKind, int), IDocumentMigration>();

        foreach (IDocumentMigration migration in migrations)
        {
            if (!migrationsByKindAndVersion.TryAdd((migration.Kind, migration.FromVersion), migration))
            {
                throw new ArgumentException(
                    $"Duplicate migration for {migration.Kind} from version {migration.FromVersion}.", nameof(migrations));
            }
        }

        int highestSupported = CurrentSchemaVersion;

        foreach (IGrouping<DocumentKind, IDocumentMigration> kindGroup in migrations.GroupBy(m => m.Kind))
        {
            List<int> fromVersions = kindGroup.Select(m => m.FromVersion).OrderBy(v => v).ToList();
            for (int i = 0; i < fromVersions.Count; i++)
            {
                int expected = i + 1;
                if (fromVersions[i] != expected)
                {
                    throw new ArgumentException(
                        $"Migrations for {kindGroup.Key} have a gap: expected a migration from version {expected}, found one from {fromVersions[i]}.",
                        nameof(migrations));
                }
            }

            highestSupported = Math.Max(highestSupported, fromVersions.Count + 1);
        }

        Supported = highestSupported;
    }

    /// <summary>
    /// The highest schema version this migrator can upgrade a document to (<see cref="CurrentSchemaVersion"/>,
    /// or higher if a longer migration chain is registered for some document kind).
    /// </summary>
    public int Supported { get; }

    /// <summary>
    /// Upgrades <paramref name="document"/> in place to <see cref="Supported"/>, applying every
    /// registered migration for <paramref name="kind"/> in order, and returns the same instance.
    /// </summary>
    /// <param name="document">Document to upgrade, mutated in place.</param>
    /// <param name="kind">The document's kind.</param>
    /// <param name="id">Document id, used in exception messages.</param>
    /// <returns><paramref name="document"/>.</returns>
    /// <exception cref="DocumentFormatException"><c>schemaVersion</c> is missing, not an integer, or
    /// less than 1.</exception>
    /// <exception cref="DocumentVersionException"><c>schemaVersion</c> is greater than <see cref="Supported"/>.</exception>
    public JsonObject Upgrade(JsonObject document, DocumentKind kind, DocumentId id)
    {
        int version = ReadSchemaVersion(document, id);

        if (version > Supported)
        {
            throw new DocumentVersionException(version, Supported, id);
        }

        while (version < Supported)
        {
            if (!migrationsByKindAndVersion.TryGetValue((kind, version), out IDocumentMigration? migration))
            {
                break;
            }

            migration.Migrate(document);
            version++;
        }

        return document;
    }

    private static int ReadSchemaVersion(JsonObject document, DocumentId id)
    {
        if (!document.TryGetPropertyValue("schemaVersion", out JsonNode? versionNode) || versionNode is null)
        {
            throw new DocumentFormatException("Missing 'schemaVersion'.", id);
        }

        int version;
        try
        {
            version = versionNode.GetValue<int>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new DocumentFormatException("'schemaVersion' must be an integer.", id, inner: ex);
        }

        if (version < 1)
        {
            throw new DocumentFormatException($"Invalid schemaVersion {version}.", id);
        }

        return version;
    }
}
