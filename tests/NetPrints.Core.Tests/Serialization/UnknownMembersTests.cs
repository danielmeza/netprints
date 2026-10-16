using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// FR-007 (research R7): members the reader does not know survive a load and save, after the known members, in ordinal key order.
    /// </summary>
    public class UnknownMembersTests
    {
        private static readonly string FixturePath = Path.Combine(
            SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld", "HelloWorld.Program.netpc.json");

        private static JsonDocumentFormat NewFormat() =>
            new(new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])),
                new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));

        private static JsonObject Fixture() => JsonNode.Parse(File.ReadAllText(FixturePath))?.AsObject()
            ?? throw new InvalidOperationException("The fixture is not a JSON object.");

        private static JsonObject Method(JsonObject root) => root["methods"]?[0]?.AsObject() ?? throw new InvalidOperationException("No method.");

        private static JsonObject Graph(JsonObject root) => Method(root)["graph"]?.AsObject() ?? throw new InvalidOperationException("No graph.");

        private static JsonObject Call(JsonObject root) => Graph(root)["nodes"]?[2]?.AsObject() ?? throw new InvalidOperationException("No call node.");

        private static JsonObject WithUnknownMembers()
        {
            JsonObject root = Fixture();
            root["zeta"] = 1;
            root["alpha"] = new JsonObject { ["b"] = new JsonArray(1, 2), ["a"] = "x" };
            root["classGraph"]?.AsObject().Add("futureGraph", true);
            Method(root)["since"] = "0.9";
            Graph(root)["note"] = new JsonObject { ["k"] = null };
            Call(root)["costHint"] = 3;
            Call(root)["pins"]?[0]?.AsObject().Add("origin", "ext");
            Graph(root)["connections"]?[0]?.AsObject().Add("style", "dashed");
            return root;
        }

        private static async Task<ClassDocument> ReadAsync(JsonObject root)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
            return await NewFormat().ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken);
        }

        private static async Task<string> WriteAsync(ClassDocument document)
        {
            using var output = new MemoryStream();
            await NewFormat().WriteClassAsync(document, output, TestContext.Current.CancellationToken);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        private static string[] Keys(JsonNode? node) => [.. node?.AsObject().Select(pair => pair.Key) ?? []];

        // US1 scenario 6: unknown members at several levels are written back.
        [Fact]
        public async Task UnknownMembersAtSeveralLevelsSurviveALoadAndSave()
        {
            string written = await WriteAsync(await ReadAsync(WithUnknownMembers()));

            JsonObject saved = JsonNode.Parse(written)?.AsObject() ?? throw new InvalidOperationException("Not an object.");
            Assert.Equal(1, (int?)saved["zeta"]);
            Assert.Equal("x", (string?)saved["alpha"]?["a"]);
            Assert.Equal(2, saved["alpha"]?["b"]?.AsArray().Count);
            Assert.True((bool?)saved["classGraph"]?["futureGraph"]);
            Assert.Equal("0.9", (string?)Method(saved)["since"]);
            Assert.True(Graph(saved).ContainsKey("note"));
            Assert.Equal(3, (int?)Call(saved)["costHint"]);
            Assert.Equal("ext", (string?)Call(saved)["pins"]?[0]?["origin"]);
            Assert.Equal("dashed", (string?)Graph(saved)["connections"]?[0]?["style"]);
        }

        // The unknown members come after the known ones, in ordinal key order.
        [Fact]
        public async Task UnknownMembersAreWrittenAfterTheKnownOnesInOrdinalKeyOrder()
        {
            string written = await WriteAsync(await ReadAsync(WithUnknownMembers()));

            JsonObject saved = JsonNode.Parse(written)?.AsObject() ?? throw new InvalidOperationException("Not an object.");
            string[] rootKeys = Keys(saved);
            Assert.Equal(["alpha", "zeta"], rootKeys[^2..]);
            Assert.Equal("layout", rootKeys[^3]);
            string[] callKeys = Keys(Call(saved));
            Assert.Equal("costHint", callKeys[^1]);
            Assert.Equal("method", callKeys[^2]);
        }

        // The saved form is a fixed point: a second load and save changes nothing.
        [Fact]
        public async Task ALoadAndSaveOfTheSavedFormIsByteIdentical()
        {
            string first = await WriteAsync(await ReadAsync(WithUnknownMembers()));

            string second = await WriteAsync(await ReadAsync(JsonNode.Parse(first)?.AsObject() ?? throw new InvalidOperationException("Not an object.")));

            Assert.Equal(first, second);
        }

        // A document without unknown members is written exactly as before.
        [Fact]
        public async Task ADocumentWithoutUnknownMembersIsUnchanged()
        {
            string original = File.ReadAllText(FixturePath);

            string written = await WriteAsync(await ReadAsync(Fixture()));

            Assert.Equal(original, written);
        }

        // An unknown member compares by value: two loads of the same text are equal.
        [Fact]
        public async Task DocumentsWithTheSameUnknownMembersAreEqual()
        {
            ClassDocument left = await ReadAsync(WithUnknownMembers());
            ClassDocument right = await ReadAsync(WithUnknownMembers());

            Assert.NotNull(left.ExtensionData);
            Assert.Equal(left.ExtensionData, right.ExtensionData);
            Assert.NotEqual(left.ExtensionData, (await ReadAsync(Fixture())).ExtensionData);
            Assert.Equal(left.ClassGraph.ExtensionData, right.ClassGraph.ExtensionData);
        }
    }
}
