using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// US8 (FR-073, T083): the arguments of a custom event entry are an optional property of the
    /// <c>eventEntry</c> node in graph schema v1: written when there are any, omitted otherwise.
    /// </summary>
    public class EventArgumentsSerializationTests
    {
        private const string FixtureName = "EventArguments.Combat.netpc.json";

        private static readonly EventArgument[] HitArguments =
        [
            new EventArgument("amount", TypeSpecifier.FromType<int>()),
            new EventArgument("source", TypeSpecifier.FromType<string>()),
            new EventArgument("critical", TypeSpecifier.FromType<bool>()),
        ];

        private static string FixturePath() => Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "EventArguments", FixtureName);

        private static NodeDocumentConverterRegistry NewRegistry() => new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static JsonDocumentFormat NewJsonFormat(NodeDocumentConverterRegistry registry) =>
            new(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));

        private static ClassGraph BuildCombat()
        {
            var cls = new ClassGraph { Name = "Combat", Namespace = "EventArguments", Visibility = MemberVisibility.Public };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            new EventEntryNode(events, "OnReset");
            new EventEntryNode(events, "OnHit").SetArguments(HitArguments);
            return cls;
        }

        private static async Task<string> SaveAsync(ClassGraph cls)
        {
            cls.MarkDirty();
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            using var output = new MemoryStream();
            await NewJsonFormat(registry).WriteClassAsync(mapper.ToDocument(cls), output, TestContext.Current.CancellationToken);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        private static async Task<ClassGraph> LoadAsync(string json, List<DocumentIssue> issues)
        {
            NodeDocumentConverterRegistry registry = NewRegistry();
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            var id = new DocumentId(FixtureName);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            ClassDocument document = await NewJsonFormat(registry).ReadClassAsync(input, id, TestContext.Current.CancellationToken);
            return mapper.FromDocument(document, TestProjects.Create("EventArguments", "EventArguments"), issues, id);
        }

        private static JsonObject Obj(JsonNode? node) => node as JsonObject ?? throw new InvalidOperationException("Expected a JSON object");

        private static JsonArray Arr(JsonNode? node) => node as JsonArray ?? throw new InvalidOperationException("Expected a JSON array");

        private static string? Text(JsonNode? node) => node is JsonValue value ? value.GetValue<string>() : null;

        private static List<JsonObject> EntryNodes(JsonNode root) =>
            Arr(Obj(root)["eventGraphs"])
                .SelectMany(graph => Arr(Obj(Obj(graph)["graph"])["nodes"]))
                .Select(Obj)
                .Where(node => Text(node["$kind"]) == "eventEntry")
                .ToList();

        private static JsonObject Entry(JsonNode root, string eventName) =>
            EntryNodes(root).Single(node => Text(node["eventName"]) == eventName);

        [Fact]
        public async Task TheArgumentsAreWrittenAndReadBack()
        {
            string json = await SaveAsync(BuildCombat());

            JsonArray written = Arr(Entry(JsonNode.Parse(json) ?? throw new InvalidOperationException(), "OnHit")["arguments"]);
            Assert.Equal(["amount", "source", "critical"], written.Select(a => Text(Obj(a)["name"])));
            Assert.Equal(["System.Int32", "System.String", "System.Boolean"], written.Select(a => Text(Obj(Obj(a)["type"])["name"])));

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = await LoadAsync(json, issues);

            Assert.Empty(issues);
            EventEntryNode entry = loaded.EventGraphs.Single().Nodes.OfType<EventEntryNode>().Single(e => e.EventName == "OnHit");
            Assert.Equal(HitArguments, entry.Arguments);
        }

        [Fact]
        public async Task AnEntryWithoutArgumentsWritesNoArgumentsProperty()
        {
            string json = await SaveAsync(BuildCombat());

            JsonObject reset = Entry(JsonNode.Parse(json) ?? throw new InvalidOperationException(), "OnReset");
            Assert.False(reset.ContainsKey("arguments"));
            Assert.False(reset.ContainsKey("argumentCount"));
        }

        [Fact]
        public async Task ArgumentsThatAreAllObjectWriteNoArgumentsProperty()
        {
            ClassGraph cls = BuildCombat();
            cls.EventGraphs.Single().Nodes.OfType<EventEntryNode>().Single(e => e.EventName == "OnHit").SetArguments(
            [
                new EventArgument("first", TypeSpecifier.FromType<object>()),
                new EventArgument("second", TypeSpecifier.FromType<object>()),
            ]);

            string json = await SaveAsync(cls);

            JsonObject hit = Entry(JsonNode.Parse(json) ?? throw new InvalidOperationException(), "OnHit");
            Assert.False(hit.ContainsKey("arguments"));
            Assert.Equal(2, (int)(hit["argumentCount"] ?? throw new InvalidOperationException()));
        }

        [Fact]
        public async Task AFileWithOnlyTheArgumentCountStillLoads()
        {
            JsonNode root = JsonNode.Parse(await SaveAsync(BuildCombat())) ?? throw new InvalidOperationException();
            Entry(root, "OnHit").Remove("arguments");

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = await LoadAsync(root.ToJsonString(), issues);

            Assert.Empty(issues);
            EventEntryNode entry = loaded.EventGraphs.Single().Nodes.OfType<EventEntryNode>().Single(e => e.EventName == "OnHit");
            Assert.Equal(3, entry.Arguments.Count);
        }

        [Fact]
        public async Task TheFixtureLoadsAndSavesByteIdentical()
        {
            if (Environment.GetEnvironmentVariable(SchemaTests.UpdateSnapshotsVariable) == "1")
            {
                await File.WriteAllTextAsync(FixturePath(), await SaveAsync(BuildCombat()), TestContext.Current.CancellationToken);
            }

            string fixture = await File.ReadAllTextAsync(FixturePath(), TestContext.Current.CancellationToken);

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = await LoadAsync(fixture, issues);

            Assert.Empty(issues);
            Assert.Equal(fixture, await SaveAsync(loaded));
        }
    }
}
