using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>DF-T24: <see cref="NetPrintsJsonSchema"/> and the committed <c>schemas/netpc.v1.schema.json</c>.</summary>
    public class SchemaTests
    {
        public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

        private static string SchemaPath() => Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "schemas", "netpc.v1.schema.json");

        [Fact]
        public void GeneratedSchemaMatchesCommittedFile()
        {
            string generated = NetPrintsJsonSchema.GenerateV1();
            string path = SchemaPath();
            bool update = Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1";

            if (update)
            {
                string? directory = Path.GetDirectoryName(path);
                Assert.NotNull(directory);
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, generated);
            }

            Assert.True(File.Exists(path), $"Missing schema file {path}; regenerate with {UpdateSnapshotsVariable}=1");
            string committed = File.ReadAllText(path);
            Assert.Equal(committed, generated);
        }

        [Fact]
        public void RootDeclaresItsOwnUrlAsId()
        {
            JsonObject schema = ParseGenerated();
            Assert.Equal(NetPrintsSchema.V1Url, Value<string>(schema, "$id"));
        }

        [Fact]
        public void NodeDocumentHasOneAnyOfBranchPerBuiltInKindPlusExtensions()
        {
            JsonObject schema = ParseGenerated();
            JsonArray anyOf = Array(NodesItemsSchema(schema), "anyOf");
            var branches = anyOf.Select(b => AsObject(b)).ToList();
            var kindSchemas = branches.Select(b => Child(Child(b, "properties"), "$kind")).ToList();

            int builtInKindCount = typeof(NodeDocument).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false).Length;
            Assert.Equal(builtInKindCount, kindSchemas.Count(k => k.ContainsKey("const")));

            JsonObject extensionKind = Assert.Single(kindSchemas, k => !k.ContainsKey("const"));
            Assert.Equal("/", Value<string>(extensionKind, "pattern"));

            JsonObject extensionBranch = Assert.Single(branches, b => !Child(Child(b, "properties"), "$kind").ContainsKey("const"));
            Assert.Equal(["$kind", "id"], Array(extensionBranch, "required").Select(n => Value<string>(n)));
        }

        [Fact]
        public void SchemaVersionIsPinnedToOne()
        {
            JsonObject schema = ParseGenerated();
            JsonObject schemaVersion = Child(Child(schema, "properties"), "schemaVersion");
            Assert.Equal(1, Value<int>(schemaVersion, "const"));
        }

        [Fact]
        public void LayoutPositionsAreTwoElementArrays()
        {
            JsonObject schema = ParseGenerated();
            JsonObject layout = Child(Child(schema, "properties"), "layout");
            JsonObject position = Child(Child(layout, "additionalProperties"), "additionalProperties");
            Assert.Equal(2, Value<int>(position, "minItems"));
            Assert.Equal(2, Value<int>(position, "maxItems"));
        }

        [Fact]
        public void MethodDocumentRequiresIdNameVisibilityAndGraphOnly()
        {
            JsonObject schema = ParseGenerated();
            JsonObject methods = Child(Child(schema, "properties"), "methods");
            JsonObject method = Child(methods, "items");
            Assert.Equal(["id", "name", "visibility", "graph"], Array(method, "required").Select(n => Value<string>(n)));
        }

        private static JsonObject ParseGenerated()
        {
            JsonNode? parsed = JsonNode.Parse(NetPrintsJsonSchema.GenerateV1());
            Assert.NotNull(parsed);
            return parsed.AsObject();
        }

        private static JsonObject NodesItemsSchema(JsonObject schema)
        {
            JsonObject classGraph = Child(Child(schema, "properties"), "classGraph");
            JsonObject nodes = Child(Child(classGraph, "properties"), "nodes");
            return Child(nodes, "items");
        }

        /// <summary>Returns <paramref name="parent"/>'s <paramref name="name"/> property as an object,
        /// failing the test with a clear message instead of a null-forgiving operator if it is absent.</summary>
        private static JsonObject Child(JsonObject parent, string name) => AsObject(GetProperty(parent, name));

        private static JsonArray Array(JsonObject parent, string name) => AsArray(GetProperty(parent, name));

        private static T Value<T>(JsonObject parent, string name) => GetValue<T>(GetProperty(parent, name));

        private static T Value<T>(JsonNode? node) => GetValue<T>(node);

        private static JsonNode GetProperty(JsonObject parent, string name)
        {
            JsonNode? node = parent[name];
            Assert.NotNull(node);
            return node;
        }

        private static JsonObject AsObject(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.AsObject();
        }

        private static JsonArray AsArray(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.AsArray();
        }

        private static T GetValue<T>(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.GetValue<T>();
        }
    }
}
