using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NetPrints.Serialization.Json;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>DF-T04: <see cref="CanonicalJsonWriter"/>'s formatting rules (document-format.md §2.3.1).</summary>
    public class CanonicalJsonTests
    {
        private static string WriteToString(JsonObject root)
        {
            using var stream = new MemoryStream();
            CanonicalJsonWriter.Write(root, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

        [Fact]
        public void HandcraftedDocumentMatchesExpectedCanonicalForm()
        {
            var root = Parse("""
            {
              "$schema": "https://example/netpc.v1.schema.json",
              "schemaVersion": 1,
              "name": "Sample",
              "visibility": "Public",
              "classGraph": { "nodes": [ { "$kind": "classReturn", "id": "n0" } ] },
              "methods": [
                {
                  "id": "m000001",
                  "name": "Main",
                  "visibility": "Public",
                  "graph": {
                    "nodes": [
                      { "$kind": "methodEntry", "id": "n0" },
                      { "$kind": "callMethod", "id": "n1", "name": "Greeting",
                        "pins": [ { "pin": "in.data.value", "value": { "type": "System.String", "value": "Quote \" and\nnewline List<T> é" } } ],
                        "method": {
                          "name": "WriteLine",
                          "declaringType": { "name": "System.Console" },
                          "parameters": [ { "name": "value", "type": { "name": "System.String" } } ]
                        }
                      }
                    ],
                    "connections": [ { "from": "n0/out.exec.Exec", "to": "n1/in.exec.Exec" } ],
                    "locals": [ { "name": "count", "type": { "name": "System.Int32" } } ]
                  }
                }
              ],
              "layout": {
                "class": { "n0": [112, 112] },
                "m000001": { "n0": [1, 2], "n1": [421, 5] }
              }
            }
            """);

            string expected =
                "{\n" +
                "  \"$schema\": \"https://example/netpc.v1.schema.json\",\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"name\": \"Sample\",\n" +
                "  \"visibility\": \"Public\",\n" +
                "  \"classGraph\": {\n" +
                "    \"nodes\": [\n" +
                "      { \"$kind\": \"classReturn\", \"id\": \"n0\" }\n" +
                "    ]\n" +
                "  },\n" +
                "  \"methods\": [\n" +
                "    {\n" +
                "      \"id\": \"m000001\",\n" +
                "      \"name\": \"Main\",\n" +
                "      \"visibility\": \"Public\",\n" +
                "      \"graph\": {\n" +
                "        \"nodes\": [\n" +
                "          { \"$kind\": \"methodEntry\", \"id\": \"n0\" },\n" +
                "          {\n" +
                "            \"$kind\": \"callMethod\",\n" +
                "            \"id\": \"n1\",\n" +
                "            \"name\": \"Greeting\",\n" +
                "            \"pins\": [\n" +
                "              { \"pin\": \"in.data.value\", \"value\": { \"type\": \"System.String\", \"value\": \"Quote \\\" and\\nnewline List<T> é\" } }\n" +
                "            ],\n" +
                "            \"method\": {\n" +
                "              \"name\": \"WriteLine\",\n" +
                "              \"declaringType\": { \"name\": \"System.Console\" },\n" +
                "              \"parameters\": [\n" +
                "                { \"name\": \"value\", \"type\": { \"name\": \"System.String\" } }\n" +
                "              ]\n" +
                "            }\n" +
                "          }\n" +
                "        ],\n" +
                "        \"connections\": [\n" +
                "          { \"from\": \"n0/out.exec.Exec\", \"to\": \"n1/in.exec.Exec\" }\n" +
                "        ],\n" +
                "        \"locals\": [\n" +
                "          { \"name\": \"count\", \"type\": { \"name\": \"System.Int32\" } }\n" +
                "        ]\n" +
                "      }\n" +
                "    }\n" +
                "  ],\n" +
                "  \"layout\": {\n" +
                "    \"class\": {\n" +
                "      \"n0\": [112, 112]\n" +
                "    },\n" +
                "    \"m000001\": {\n" +
                "      \"n0\": [1, 2],\n" +
                "      \"n1\": [421, 5]\n" +
                "    }\n" +
                "  }\n" +
                "}\n";

            string actual = WriteToString(root);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OutputHasNoBomAndEndsWithExactlyOneNewline()
        {
            string actual = WriteToString(Parse("""{"a":1}"""));

            Assert.EndsWith("\n", actual);
            Assert.DoesNotContain("\r", actual);
            Assert.False(actual.EndsWith("\n\n", StringComparison.Ordinal));

            using var stream = new MemoryStream();
            CanonicalJsonWriter.Write(Parse("""{"a":1}"""), stream);
            byte[] bytes = stream.ToArray();
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "Output must not start with a UTF-8 BOM.");
        }

        [Fact]
        public void OutputReParses()
        {
            var root = Parse("""
            { "$schema": "s", "schemaVersion": 1, "classGraph": { "nodes": [] },
              "layout": { "class": { "n0": [1, 2] } } }
            """);

            string actual = WriteToString(root);
            JsonNode? reparsed = JsonNode.Parse(actual);

            Assert.NotNull(reparsed);
            Assert.True(JsonNode.DeepEquals(root, reparsed));
        }

        [Fact]
        public void EmptyObjectAndArrayAreWrittenInline()
        {
            var root = Parse("""{ "emptyObject": {}, "emptyArray": [] }""");

            string actual = WriteToString(root);

            Assert.Equal("{\n  \"emptyObject\": {},\n  \"emptyArray\": []\n}\n", actual);
        }

        [Fact]
        public void NonFiniteNumberThrowsArgumentException()
        {
            var root = new JsonObject { ["value"] = JsonValue.Create(double.NaN) };

            Assert.Throws<ArgumentException>(() => WriteToString(root));
        }

        [Theory]
        [InlineData("connections")]
        [InlineData("pins")]
        [InlineData("locals")]
        [InlineData("parameters")]
        [InlineData("args")]
        [InlineData("returnTypes")]
        [InlineData("genericArgs")]
        [InlineData("dataTypes")]
        public void ElementsOfNamedArraysAreInline(string propertyName)
        {
            var root = new JsonObject { [propertyName] = new JsonArray(new JsonObject { ["a"] = 1, ["b"] = 2 }) };

            string actual = WriteToString(root);

            Assert.Equal($"{{\n  \"{propertyName}\": [\n    {{ \"a\": 1, \"b\": 2 }}\n  ]\n}}\n", actual);
        }

        [Theory]
        [InlineData("declaringType")]
        [InlineData("type")]
        [InlineData("literalType")]
        [InlineData("value")]
        [InlineData("default")]
        public void NamedObjectPropertiesAreInline(string propertyName)
        {
            var root = new JsonObject { [propertyName] = new JsonObject { ["a"] = 1, ["b"] = 2 } };

            string actual = WriteToString(root);

            Assert.Equal($"{{\n  \"{propertyName}\": {{ \"a\": 1, \"b\": 2 }}\n}}\n", actual);
        }

        [Fact]
        public void NodesArrayElementWithOnlyCommonFieldsIsInline()
        {
            var root = new JsonObject
            {
                ["nodes"] = new JsonArray(
                    new JsonObject { ["$kind"] = "literal", ["id"] = "n0", ["name"] = "X" },
                    new JsonObject { ["$kind"] = "literal", ["id"] = "n1", ["extra"] = true }),
            };

            string actual = WriteToString(root);

            Assert.Equal(
                "{\n  \"nodes\": [\n    { \"$kind\": \"literal\", \"id\": \"n0\", \"name\": \"X\" },\n" +
                "    {\n      \"$kind\": \"literal\",\n      \"id\": \"n1\",\n      \"extra\": true\n    }\n  ]\n}\n",
                actual);
        }

        [Fact]
        public void NestedValueInsideInlineValueStaysInline()
        {
            var root = new JsonObject
            {
                ["type"] = new JsonObject { ["name"] = "System.List", ["args"] = new JsonArray(new JsonObject { ["name"] = "T" }) },
            };

            string actual = WriteToString(root);

            Assert.Equal("{\n  \"type\": { \"name\": \"System.List\", \"args\": [{ \"name\": \"T\" }] }\n}\n", actual);
        }

        [Fact]
        public void PropertiesAreNeverReordered()
        {
            var root = new JsonObject { ["z"] = 1, ["a"] = 2, ["m"] = 3 };

            string actual = WriteToString(root);

            Assert.Equal("{\n  \"z\": 1,\n  \"a\": 2,\n  \"m\": 3\n}\n", actual);
        }
    }
}
