using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// <see cref="JsonDocumentFormat"/>'s read/write pipeline (document-format.md §2.2): DF-T09
    /// (malformed JSON), DF-T21 (tolerant read, canonical write).
    /// </summary>
    public class JsonDocumentFormatTests
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])).SerializerOptions;

        private static readonly ClassDocument SampleDocument = new(
            DocumentMigrator.CurrentSchemaVersion, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
            new GraphDocument([new ClassReturnNodeDocument("n0", null, null, 0)], null, null),
            null, null, null, null, null);

        private static JsonDocumentFormat NewFormat() =>
            new(new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])),
                new DocumentMigrator([]));

        private static MemoryStream Utf8Stream(string text) => new(Encoding.UTF8.GetBytes(text));

        private static async Task<string> WriteCanonicalAsync(ClassDocument document)
        {
            JsonDocumentFormat format = NewFormat();
            using var output = new MemoryStream();
            await format.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        // DF-T09: malformed JSON syntax fails with a line and byte position.
        [Fact]
        public async Task MalformedJsonSyntaxThrowsWithLineAndPosition()
        {
            JsonDocumentFormat format = NewFormat();
            using var input = Utf8Stream("{\n  \"schemaVersion\": 1,\n  \"name\": ,\n}");

            DocumentFormatException ex = await Assert.ThrowsAsync<DocumentFormatException>(async () =>
                await format.ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken));

            Assert.NotNull(ex.Line);
            Assert.NotNull(ex.BytePosition);
        }

        // DF-T09: a structurally invalid document (valid JSON, wrong shape) fails with the JSON path in
        // the message, and no Line/BytePosition (only syntax errors carry those).
        [Fact]
        public async Task StructurallyInvalidDocumentThrowsWithJsonPathInMessage()
        {
            string canonical = await WriteCanonicalAsync(SampleDocument);
            string corrupted = canonical.Replace("\"name\": \"C\"", "\"name\": 123");
            Assert.NotEqual(canonical, corrupted);

            JsonDocumentFormat format = NewFormat();
            using var input = Utf8Stream(corrupted);

            DocumentFormatException ex = await Assert.ThrowsAsync<DocumentFormatException>(async () =>
                await format.ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken));

            Assert.Null(ex.Line);
            Assert.Contains("name", ex.Message);
        }

        // DF-T21: a $schema value that is not a string fails to load.
        [Fact]
        public async Task NonStringSchemaPropertyThrows()
        {
            string canonical = await WriteCanonicalAsync(SampleDocument);
            string withBadSchema = canonical.Replace(
                $"\"$schema\": \"{NetPrintsSchema.V1Url}\"", "\"$schema\": 1");
            Assert.NotEqual(canonical, withBadSchema);

            JsonDocumentFormat format = NewFormat();
            using var input = Utf8Stream(withBadSchema);

            await Assert.ThrowsAsync<DocumentFormatException>(async () =>
                await format.ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken));
        }

        // DF-T21: comments, trailing commas, a $kind after other properties, no $schema, reordered
        // top-level properties and arbitrary whitespace load to the same model as the canonical form,
        // and writing the result back out reproduces the canonical bytes.
        [Fact]
        public async Task TolerantReadLoadsSameModelAndWritesCanonicalForm()
        {
            string canonical = await WriteCanonicalAsync(SampleDocument);

            const string messy = """
                {
                    // A leading comment; no $schema at all.
                    "name"   :    "C",
                    "classGraph": {
                        "nodes": [
                            {
                                "id": "n0", /* $kind moved after id */
                                "$kind": "classReturn",
                            },
                        ],
                    },
                    "visibility": "public",
                    "schemaVersion": 1,
                }
                """;

            JsonDocumentFormat format = NewFormat();

            using var canonicalInput = Utf8Stream(canonical);
            ClassDocument fromCanonical = await format.ReadClassAsync(
                canonicalInput, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken);

            using var messyInput = Utf8Stream(messy);
            ClassDocument fromMessy = await format.ReadClassAsync(
                messyInput, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken);

            JsonNode? canonicalNode = JsonSerializer.SerializeToNode(fromCanonical, JsonOptions);
            JsonNode? messyNode = JsonSerializer.SerializeToNode(fromMessy, JsonOptions);
            Assert.True(JsonNode.DeepEquals(canonicalNode, messyNode));

            using var output = new MemoryStream();
            await format.WriteClassAsync(fromMessy, output, TestContext.Current.CancellationToken);
            Assert.Equal(canonical, Encoding.UTF8.GetString(output.ToArray()));
        }

        // document-format.md §2.2: $schema is written first, followed by the serialized properties.
        [Fact]
        public async Task WriteInsertsSchemaFirst()
        {
            string canonical = await WriteCanonicalAsync(SampleDocument);
            Assert.StartsWith($"{{\n  \"$schema\": \"{NetPrintsSchema.V1Url}\",\n", canonical);
        }
    }
}
