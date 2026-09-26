using System.Text.Json;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>T026: <see cref="NodeListConverter"/>'s tolerant, out-of-order <c>$kind</c>/<c>id</c>
    /// lookup, unknown-kind preservation and missing-field errors.</summary>
    public class NodeListConverterTests
    {
        private static readonly JsonSerializerOptions Options =
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])).SerializerOptions;

        private static GraphDocument Deserialize(string json) =>
            JsonSerializer.Deserialize<GraphDocument>(json, Options)!;

        [Fact]
        public void KindAfterIdDeserializes()
        {
            GraphDocument graph = Deserialize("""{ "nodes": [ { "id": "n0", "$kind": "classReturn" } ] }""");

            var node = Assert.IsType<ClassReturnNodeDocument>(Assert.Single(graph.Nodes));
            Assert.Equal("n0", node.Id);
        }

        [Fact]
        public void KindAfterKindFieldsDeserializes()
        {
            GraphDocument graph = Deserialize("""{ "nodes": [ { "id": "n0", "interfaceCount": 2, "$kind": "classReturn" } ] }""");

            var node = Assert.IsType<ClassReturnNodeDocument>(Assert.Single(graph.Nodes));
            Assert.Equal("n0", node.Id);
            Assert.Equal(2, node.InterfaceCount);
        }

        [Fact]
        public void UnknownKindBecomesUnknownNodeDocument()
        {
            GraphDocument graph = Deserialize("""{ "nodes": [ { "$kind": "test.ext/widget", "id": "n1", "extra": 42 } ] }""");

            var node = Assert.IsType<UnknownNodeDocument>(Assert.Single(graph.Nodes));
            Assert.Equal("n1", node.Id);
            Assert.Equal("test.ext/widget", node.Kind);
            Assert.Equal(42, node.Raw.GetProperty("extra").GetInt32());
        }

        [Fact]
        public void UnknownKindReemitsKindAndIdFirstThenSourceOrder()
        {
            GraphDocument graph = Deserialize("""{ "nodes": [ { "extra": 42, "id": "n1", "$kind": "test.ext/widget", "more": "x" } ] }""");

            string written = JsonSerializer.Serialize(graph, Options);
            using JsonDocument reparsed = JsonDocument.Parse(written);
            JsonElement node = reparsed.RootElement.GetProperty("nodes")[0];
            var propertyNames = new System.Collections.Generic.List<string>();
            foreach (JsonProperty property in node.EnumerateObject())
            {
                propertyNames.Add(property.Name);
            }

            Assert.Equal(["$kind", "id", "extra", "more"], propertyNames);
        }

        [Fact]
        public void MissingKindThrowsDocumentFormatException()
        {
            Assert.Throws<DocumentFormatException>(() => Deserialize("""{ "nodes": [ { "id": "n0" } ] }"""));
        }

        [Fact]
        public void MissingIdThrowsDocumentFormatException()
        {
            Assert.Throws<DocumentFormatException>(() => Deserialize("""{ "nodes": [ { "$kind": "classReturn" } ] }"""));
        }
    }
}
