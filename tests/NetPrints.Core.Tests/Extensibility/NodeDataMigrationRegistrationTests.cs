using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Serialization.Migrations;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-026, NPXE0013: the migrations an extension contributes reach the registry's node migrator, with the manifest prefix rule.</summary>
public class NodeDataMigrationRegistrationTests
{
    private sealed class SetLabel(string nodeKind, int fromVersion) : INodeDataMigration
    {
        public string NodeKind => nodeKind;

        public int FromVersion => fromVersion;

        public void Migrate(JsonObject node) => node["label"] = "migrated";
    }

    private static JsonObject Document(string kind) =>
        new() { ["classGraph"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["$kind"] = kind, ["id"] = "n0000000000ab1" }) } };

    private static JsonObject FirstNode(JsonObject document)
    {
        JsonObject graph = Assert.IsType<JsonObject>(document["classGraph"]);
        JsonArray nodes = Assert.IsType<JsonArray>(graph["nodes"]);
        return Assert.IsType<JsonObject>(nodes[0]);
    }

    [Fact]
    public async Task AMigrationOfTheExtensionsOwnKindUpgradesTheNodesOfThatKind()
    {
        var extension = InProcess("fx", builder => builder
            .AddNodeLibrary(new SingleKindLibrary("fx", PingKind("fx/Ping") with { DataVersion = 1 }))
            .AddDocumentMigration(new SetLabel("fx/Ping", 0)));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        JsonObject upgraded = registry.NodeMigrator.Upgrade(Document("fx/Ping"));
        Assert.Empty(registry.Issues);
        Assert.Equal("migrated", FirstNode(upgraded)["label"]?.GetValue<string>());
        Assert.Equal(1, FirstNode(upgraded)["kindVersion"]?.GetValue<int>());
        Assert.Equal(1, registry.NodeConverters.DataVersionOf("fx/Ping"));
    }

    [Fact]
    public async Task AMigrationOfAKindWithoutTheManifestPrefixIsNpx013AndIsDropped()
    {
        var extension = InProcess("fx", builder => builder
            .AddNodeLibrary(new SingleKindLibrary("fx", PingKind("fx/Ping") with { DataVersion = 1 }))
            .AddDocumentMigration(new SetLabel("other/Ping", 0))
            .AddDocumentMigration(new SetLabel("fx", 0)));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Equal(2, registry.Issues.Count);
        Assert.All(registry.Issues, issue => Assert.Equal(ExtensionDiagnosticCodes.IdPrefix, issue.Code));
        Assert.Equal("NPX013", registry.Issues[0].Code);
        JsonObject upgraded = registry.NodeMigrator.Upgrade(Document("fx/Ping"));
        Assert.Null(FirstNode(upgraded)["label"]);
    }
}
