#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;
using NetPrints.Core;

namespace NetPrints.Serialization.Migrations;

/// <summary>
/// Upgrades the node objects of a document whose kind declares a data version: the migrations of the kind run in
/// <see cref="INodeDataMigration.FromVersion"/> order from the stored <c>kindVersion</c> (absent is 0) up to the current one (research R18).
/// </summary>
[Experimental(ExperimentalApiIds.NodeDataMigrations, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed class NodeDataMigrator
{
    /// <summary>The name of the member that stores a node's data version.</summary>
    public const string KindVersionMember = "kindVersion";

    private const string KindMember = "$kind";

    private readonly Dictionary<string, INodeDataMigration[]> migrationsByKind;
    private readonly IReadOnlyDictionary<string, int> dataVersions;

    /// <summary>
    /// Creates a migrator.
    /// </summary>
    /// <param name="migrations">The registered migrations.</param>
    /// <param name="dataVersions">The current data version of each node kind that declares one above 0.</param>
    /// <exception cref="ArgumentException">Two migrations share a node kind and a <see cref="INodeDataMigration.FromVersion"/>.</exception>
    public NodeDataMigrator(IReadOnlyList<INodeDataMigration> migrations, IReadOnlyDictionary<string, int> dataVersions)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(dataVersions);
        this.dataVersions = dataVersions;
        migrationsByKind = migrations
            .GroupBy(migration => migration.NodeKind, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(migration => migration.FromVersion).ToArray(),
                StringComparer.Ordinal);

        foreach ((string kind, INodeDataMigration[] ordered) in migrationsByKind)
        {
            if (ordered.Select(migration => migration.FromVersion).Distinct().Count() != ordered.Length)
            {
                throw new ArgumentException($"Duplicate node data migration for '{kind}' from the same version.", nameof(migrations));
            }
        }
    }

    /// <summary>
    /// Upgrades every node object in <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The parsed document, mutated in place.</param>
    /// <returns><paramref name="document"/>.</returns>
    public JsonObject Upgrade(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (dataVersions.Count > 0)
        {
            Visit(document);
        }

        return document;
    }

    private void Visit(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                UpgradeNode(obj);
                foreach (JsonNode? child in obj.Select(member => member.Value).ToList())
                {
                    Visit(child);
                }

                break;
            case JsonArray array:
                foreach (JsonNode? child in array)
                {
                    Visit(child);
                }

                break;
        }
    }

    private void UpgradeNode(JsonObject node)
    {
        if (node[KindMember] is not JsonValue kindValue || !kindValue.TryGetValue(out string? kind)
            || !dataVersions.TryGetValue(kind, out int current))
        {
            return;
        }

        int stored = node[KindVersionMember] is JsonValue versionValue && versionValue.TryGetValue(out int parsed) ? parsed : 0;
        if (stored >= current)
        {
            return;
        }

        if (migrationsByKind.TryGetValue(kind, out INodeDataMigration[]? migrations))
        {
            foreach (INodeDataMigration migration in migrations.Where(migration => migration.FromVersion >= stored && migration.FromVersion < current))
            {
                migration.Migrate(node);
            }
        }

        node[KindVersionMember] = current;
    }
}
