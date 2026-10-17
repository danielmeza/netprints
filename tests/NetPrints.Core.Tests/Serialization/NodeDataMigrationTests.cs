using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    internal sealed record FxDataNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins, string? Label) : NodeDocument(Id, Name, Pins);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(FxDataNodeDocument))]
    internal sealed partial class FxJsonContext : JsonSerializerContext;

    /// <summary>FR-026, research R18, NPXE0013: a per-kind data version, written only above 0, and the migrations that upgrade older nodes on open.</summary>
    public sealed class NodeDataMigrationTests
    {
        private const string FxKind = "fx.ext/Data";
        private const string FixtureFile = "HelloWorld/HelloWorld.Program.netpc.json";
        private const string FxNodeId = "n0000000000fx1";

        private sealed class FxDataNode : Node
        {
            public FxDataNode(NodeGraph graph)
                : base(graph)
            {
            }
        }

        private sealed class FxDataConverter : INodeDocumentConverter
        {
            public string Kind => FxKind;

            public Type NodeType => typeof(FxDataNode);

            public Type DocumentType => typeof(FxDataNodeDocument);

            public NodeDocument ToDocument(Node node, NodeMappingContext context) => new FxDataNodeDocument(node.Id, null, null, "label");

            public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new FxDataNode(graph);
        }

        private sealed class RenameMember(int fromVersion, string from, string to, string nodeKind = FxKind) : INodeDataMigration
        {
            public string NodeKind => nodeKind;

            public int FromVersion => fromVersion;

            public void Migrate(JsonObject node)
            {
                JsonNode? value = node[from];
                node.Remove(from);
                node[to] = value;
            }
        }

        private static readonly IReadOnlyList<IJsonTypeInfoResolver> Resolvers = [FxJsonContext.Default];

        private static NodeDocumentConverterRegistry Registry(int dataVersion = 2) =>
            new([.. NodeDocumentConverterRegistry.BuiltIn, new FxDataConverter()], Resolvers, dataVersion > 0 ? new Dictionary<string, int> { [FxKind] = dataVersion } : null);

        private static INodeDataMigration[] Migrations() => [new RenameMember(0, "text", "caption"), new RenameMember(1, "caption", "label")];

        private static JsonDocumentFormat Format(NodeDocumentConverterRegistry registry, params INodeDataMigration[] migrations) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance), new NodeDataMigrator(migrations, registry.DataVersions));

        private static async Task<string> FixtureJsonAsync(JsonObject fxNode)
        {
            string path = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", FixtureFile);
            JsonObject root = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))?.AsObject()
                ?? throw new InvalidOperationException("The fixture is not a JSON object.");
            root["methods"]?[0]?["graph"]?["nodes"]?.AsArray().Add(fxNode);
            return root.ToJsonString();
        }

        private static async Task<ClassDocument> ReadAsync(JsonDocumentFormat format, string json)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return await format.ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken);
        }

        private static FxDataNodeDocument FxNodeOf(ClassDocument document) =>
            Assert.IsType<FxDataNodeDocument>(document.Methods?[0].Graph.Nodes.Single(node => node.Id == FxNodeId));

        [Fact]
        public async Task ANodeWithoutKindVersionIsVersionZeroAndMigratesOnOpen()
        {
            NodeDocumentConverterRegistry registry = Registry();
            string json = await FixtureJsonAsync(new JsonObject { ["$kind"] = FxKind, ["id"] = FxNodeId, ["text"] = "hi" });

            ClassDocument document = await ReadAsync(Format(registry, Migrations()), json);

            FxDataNodeDocument node = FxNodeOf(document);
            Assert.Equal("hi", node.Label);
            Assert.Equal(2, node.KindVersion);
        }

        [Fact]
        public async Task MigrationsRunFromTheStoredVersionOnly()
        {
            NodeDocumentConverterRegistry registry = Registry();
            string json = await FixtureJsonAsync(new JsonObject { ["$kind"] = FxKind, ["id"] = FxNodeId, ["kindVersion"] = 1, ["caption"] = "kept" });

            ClassDocument document = await ReadAsync(Format(registry, Migrations()), json);

            Assert.Equal("kept", FxNodeOf(document).Label);
        }

        [Fact]
        public async Task MigrationsRunInFromVersionOrderWhateverTheRegistrationOrder()
        {
            NodeDocumentConverterRegistry registry = Registry();
            string json = await FixtureJsonAsync(new JsonObject { ["$kind"] = FxKind, ["id"] = FxNodeId, ["text"] = "ordered" });

            ClassDocument document = await ReadAsync(Format(registry, [.. Migrations().Reverse()]), json);

            Assert.Equal("ordered", FxNodeOf(document).Label);
        }

        [Fact]
        public async Task ANodeAtTheCurrentVersionIsNotMigratedAgain()
        {
            NodeDocumentConverterRegistry registry = Registry();
            string json = await FixtureJsonAsync(new JsonObject { ["$kind"] = FxKind, ["id"] = FxNodeId, ["kindVersion"] = 2, ["label"] = "now", ["text"] = "stale" });

            ClassDocument document = await ReadAsync(Format(registry, Migrations()), json);

            Assert.Equal("now", FxNodeOf(document).Label);
        }

        [Fact]
        public async Task AMigrationOfAnotherKindDoesNotTouchTheNode()
        {
            NodeDocumentConverterRegistry registry = Registry();
            string json = await FixtureJsonAsync(new JsonObject { ["$kind"] = FxKind, ["id"] = FxNodeId, ["text"] = "hi", ["label"] = "mine" });

            ClassDocument document = await ReadAsync(Format(registry, new RenameMember(0, "text", "label", "other.ext/Data")), json);

            Assert.Equal("mine", FxNodeOf(document).Label);
        }

        [Fact]
        public void KindVersionIsWrittenOnlyWhenTheKindDeclaresAVersionAboveZero()
        {
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.csproj");
            ClassGraph original = project.Classes.Single();

            foreach (int version in new[] { 0, 2 })
            {
                NodeDocumentConverterRegistry registry = Registry(version);
                var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
                ClassDocument document = mapper.ToDocument(original);
                Assert.NotNull(document.Methods);
                MethodDocument method = document.Methods[0];
                document = document with
                {
                    Methods = [method with { Graph = method.Graph with { Nodes = [.. method.Graph.Nodes, new FxDataNodeDocument(FxNodeId, null, null, "x")] } }],
                };

                var issues = new List<DocumentIssue>();
                ClassGraph loaded = mapper.FromDocument(document, project, issues, new DocumentId("AllNodes.Everything.netpc.json"));
                ClassDocument written = mapper.ToDocument(loaded);

                Assert.NotNull(written.Methods);
                IEnumerable<NodeDocument> nodes = written.Methods.SelectMany(m => m.Graph.Nodes);
                Assert.Equal(version > 0 ? version : null, nodes.Single(n => n.Id == FxNodeId).KindVersion);
                Assert.All(nodes.Where(n => n.Id != FxNodeId), n => Assert.Null(n.KindVersion));
            }
        }
    }
}
