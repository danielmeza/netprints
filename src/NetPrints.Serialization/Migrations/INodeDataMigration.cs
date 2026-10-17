#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using NetPrints.Core;

namespace NetPrints.Serialization.Migrations;

/// <summary>
/// Upgrades the stored data of one node kind from one data version to the next (research R18). The data version of a node
/// is its <c>kindVersion</c> member (absent means 0); the current one is <c>NodeKindDescriptor.DataVersion</c>.
/// </summary>
[Experimental(ExperimentalApiIds.NodeDataMigrations, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface INodeDataMigration
{
    /// <summary>
    /// Gets the node kind this migration applies to. For an extension it starts with the manifest id and a slash.
    /// </summary>
    string NodeKind { get; }

    /// <summary>
    /// Gets the data version this migration reads; it upgrades the node to <c>FromVersion + 1</c>.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Upgrades <paramref name="node"/> in place. It may change this node's members only.
    /// </summary>
    /// <param name="node">The node's JSON object, mutated in place.</param>
    void Migrate(JsonObject node);
}
