#nullable enable
using System.Text.Json.Nodes;

namespace NetPrints.Serialization.Migrations;

/// <summary>
/// Upgrades a document in place from one schema version to the next (document-format.md §2.5).
/// </summary>
public interface IDocumentMigration
{
    /// <summary>
    /// The kind of document this migration applies to.
    /// </summary>
    DocumentKind Kind { get; }

    /// <summary>
    /// The schema version this migration reads; it upgrades the document to <c>FromVersion + 1</c>.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Upgrades <paramref name="document"/> in place from <see cref="FromVersion"/> to
    /// <c>FromVersion + 1</c>. Must set <c>"schemaVersion"</c> to the new version.
    /// </summary>
    /// <param name="document">Document to upgrade, mutated in place.</param>
    void Migrate(JsonObject document);
}
