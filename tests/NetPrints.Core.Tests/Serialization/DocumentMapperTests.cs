using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Tests.Characterization;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// <see cref="DocumentMapper"/>'s <c>ToDocument</c>/<c>FromDocument</c> round trip: member ids, pins
    /// by key, default names, edges, integer layout, auto-placement and preserved unknown nodes
    /// (document-format.md §2.6). DF-T06, DF-T08, DF-T19, DF-T20, DF-T22, DF-T25, DF-T27 (document/JSON
    /// halves only; DF-T02/T03/T05 golden-byte round trips are T042).
    /// </summary>
    public class DocumentMapperTests
    {
        private static DocumentMapper NewMapper() =>
            new(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []));

        private static readonly JsonSerializerOptions JsonOptions =
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])).SerializerOptions;

        private static GraphDocument SimpleMethodGraph(string entryId, string returnId) =>
            new([new MethodEntryNodeDocument(entryId, null, null, 0, null), new ReturnNodeDocument(returnId, null, null, 0)], null, null);

        // DF-T06: every built-in kind round-trips, including dynamic pin counts and renamed pins.
        [Fact]
        public void AllNodesRoundTripsThroughToDocumentAndFromDocument()
        {
            using var _ = IdGeneration.Use(new SeededIdGenerator(42));
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.netpp");
            ClassGraph original = project.Classes.Single();

            DocumentMapper mapper = NewMapper();
            ClassDocument first = mapper.ToDocument(original);

            var issues = new List<DocumentIssue>();
            ClassGraph rebuilt = mapper.FromDocument(first, project, issues, new DocumentId("AllNodes.Everything.netpc.json"));

            Assert.Empty(issues);
            Assert.False(rebuilt.IsDirty);

            ClassDocument second = mapper.ToDocument(rebuilt);

            JsonNode? firstNode = JsonSerializer.SerializeToNode(first, JsonOptions);
            JsonNode? secondNode = JsonSerializer.SerializeToNode(second, JsonOptions);
            Assert.True(JsonNode.DeepEquals(firstNode, secondNode));
        }

        // DF-T27 (JSON-path half): Variable.TypeGraph survives, and its resolved type is unchanged.
        [Fact]
        public void VariableTypeGraphSurvivesRoundTripWithSettledTypes()
        {
            using var _ = IdGeneration.Use(new SeededIdGenerator(99));
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.netpp");
            ClassGraph original = project.Classes.Single();
            TypeSpecifier originalItemsType = original.Variables.Single().Type;

            DocumentMapper mapper = NewMapper();
            ClassDocument document = mapper.ToDocument(original);

            var issues = new List<DocumentIssue>();
            ClassGraph rebuilt = mapper.FromDocument(document, project, issues, new DocumentId("AllNodes.Everything.netpc.json"));

            Assert.All(rebuilt.Variables, v => Assert.NotNull(v.TypeGraph));
            Assert.Equal(originalItemsType, rebuilt.Variables.Single().Type);
        }

        // DF-T08 (document part): an unknown node kind and a connection into it are preserved.
        [Fact]
        public void UnknownNodeAndItsConnectionArePreservedRoundTrip()
        {
            JsonElement raw = JsonDocument.Parse("""{"$kind":"test.ext/widget","id":"n9","extra":1}""").RootElement;
            var unknownNode = new UnknownNodeDocument("n9", "test.ext/widget", raw);
            var classReturn = new ClassReturnNodeDocument("n0", null, null, 0);
            var classGraph = new GraphDocument([classReturn, unknownNode],
                [new ConnectionDocument("n9/out.type.Whatever", "n0/in.type.BaseType")], null);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Contains(issues, i => i.Code == DocumentIssue.UnknownNodeKind);
            Assert.NotNull(cls.PreservedDocumentState);

            ClassDocument roundTripped = mapper.ToDocument(cls);
            var preservedNode = Assert.IsType<UnknownNodeDocument>(roundTripped.ClassGraph.Nodes.Single(n => n.Id == "n9"));
            Assert.Equal("test.ext/widget", preservedNode.Kind);
            ConnectionDocument preservedConnection = Assert.Single(roundTripped.ClassGraph.Connections!);
            Assert.Equal("n9/out.type.Whatever", preservedConnection.From);
            Assert.Equal("n0/in.type.BaseType", preservedConnection.To);
        }

        // DF-T19 (document part): inserting a parameter keeps the connection on the unmoved parameter
        // names and drops nothing else.
        [Fact]
        public void CallMethodParameterInsertionKeepsConnectionsByName()
        {
            var intRef = new TypeRef("System.Int32");
            var stringRef = new TypeRef("System.String");
            var declaringType = new TypeRef("C");

            MethodRef Method(params ParameterRef[] parameters) =>
                new("M", declaringType, parameters, null, MethodModifiers.Static, MemberVisibility.Public, null);

            MethodRef editedMethod = Method(
                new ParameterRef("a", intRef), new ParameterRef("inserted", stringRef), new ParameterRef("b", intRef));

            // The connection sources are literal nodes (LiteralNode.UpdatePinTypes' reference-equality
            // bug, which used to disconnect a literal's value pins on every GraphTypeInference.Relax
            // pass even with no generic arguments involved, is fixed; see implementation-notes.md,
            // "T035 - LiteralNode.UpdatePinTypes...").
            var entry = new MethodEntryNodeDocument("n3", null, null, 0, null);
            var call = new CallMethodNodeDocument("n2", null, null, editedMethod, 0);
            var literalA = new LiteralNodeDocument("n6", null, null, intRef);
            var literalB = new LiteralNodeDocument("n7", null, null, intRef);

            var connections = new List<ConnectionDocument>
            {
                new("n6/out.data.Value", "n2/in.data.a"),
                new("n7/out.data.Value", "n2/in.data.b"),
            };

            var methodGraph = new GraphDocument(
                [entry, new ReturnNodeDocument("n4", null, null, 0), call, literalA, literalB],
                connections, null);
            var methodDocument = new MethodDocument("m000001", "Caller", MemberVisibility.Public, MethodModifiers.None, methodGraph);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument("n5", null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.DoesNotContain(issues, i => i.Code == DocumentIssue.ConnectionDropped);

            CallMethodNode callNode = cls.Methods.Single().Nodes.OfType<CallMethodNode>().Single();
            Assert.Equal(3, callNode.InputDataPins.Count);
            Assert.NotNull(callNode.InputDataPins.Single(p => p.Name == "a").IncomingPin);
            Assert.NotNull(callNode.InputDataPins.Single(p => p.Name == "b").IncomingPin);
            Assert.Null(callNode.InputDataPins.Single(p => p.Name == "inserted").IncomingPin);
        }

        // DF-T20 (document part): a node with no layout entry is auto-placed; positioned nodes keep
        // their exact positions; auto-placement is deterministic across two loads of the same document.
        [Fact]
        public void NodeWithoutLayoutEntryIsAutoPlacedDeterministically()
        {
            var literal = new LiteralNodeDocument("n2", null, null, new TypeRef("System.Int32"));
            var methodGraph = new GraphDocument(
                [new MethodEntryNodeDocument("n0", null, null, 0, null), new ReturnNodeDocument("n1", null, null, 0), literal],
                [new ConnectionDocument("n0/out.exec.Exec", "n1/in.exec.Exec")], null);
            var methodDocument = new MethodDocument("m000001", "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);

            var layout = new SortedDictionary<string, SortedDictionary<string, int[]>>(System.StringComparer.Ordinal)
            {
                ["m000001"] = new SortedDictionary<string, int[]>(System.StringComparer.Ordinal)
                {
                    ["n0"] = [100, 50],
                    ["n1"] = [400, 50],
                    // n2 (the literal) intentionally has no entry: it must be auto-placed.
                },
            };

            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument("n3", null, null, 0)], null, null),
                null, [methodDocument], null, null, layout);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));

            MethodGraph method = cls.Methods.Single();
            Node n0 = method.FindNode("n0")!;
            Node n1 = method.FindNode("n1")!;
            Node literalNode = method.Nodes.OfType<LiteralNode>().Single();

            Assert.Equal(100, n0.PositionX);
            Assert.Equal(50, n0.PositionY);
            Assert.Equal(400, n1.PositionX);
            Assert.Equal(50, n1.PositionY);

            var issues2 = new List<DocumentIssue>();
            ClassGraph cls2 = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues2, new DocumentId("C.netpc.json"));
            Node literalNode2 = cls2.Methods.Single().Nodes.OfType<LiteralNode>().Single();

            Assert.Equal(literalNode2.PositionX, literalNode.PositionX);
            Assert.Equal(literalNode2.PositionY, literalNode.PositionY);
        }

        // DF-T22 (document part): a missing or duplicate member id fails the whole load.
        [Fact]
        public void MissingMemberIdThrowsDocumentFormatException()
        {
            var methodDocument = new MethodDocument(string.Empty, "M", MemberVisibility.Public, MethodModifiers.None,
                SimpleMethodGraph("n0", "n1"));
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument("n2", null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            Assert.Throws<DocumentFormatException>(() => mapper.FromDocument(
                classDocument, Project.CreateNew("P", "P"), new List<DocumentIssue>(), new DocumentId("C.netpc.json")));
        }

        [Fact]
        public void DuplicateMemberIdThrowsDocumentFormatException()
        {
            var methodA = new MethodDocument("m000001", "A", MemberVisibility.Public, MethodModifiers.None, SimpleMethodGraph("n0", "n1"));
            var methodB = new MethodDocument("m000001", "B", MemberVisibility.Public, MethodModifiers.None, SimpleMethodGraph("n2", "n3"));
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument("n4", null, null, 0)], null, null),
                null, [methodA, methodB], null, null, null);

            DocumentMapper mapper = NewMapper();
            Assert.Throws<DocumentFormatException>(() => mapper.FromDocument(
                classDocument, Project.CreateNew("P", "P"), new List<DocumentIssue>(), new DocumentId("C.netpc.json")));
        }

        // DF-T22 (document part): layout keys use member ids, unaffected by model member order.
        [Fact]
        public void LayoutKeysUseMemberIdsRegardlessOfMethodOrder()
        {
            var cls = new ClassGraph { Name = "C" };
            var methodA = new MethodGraph("A") { Class = cls };
            var methodB = new MethodGraph("B") { Class = cls };
            cls.Methods.Add(methodA);
            cls.Methods.Add(methodB);

            DocumentMapper mapper = NewMapper();
            ClassDocument before = mapper.ToDocument(cls);

            cls.Methods.Move(1, 0);
            ClassDocument after = mapper.ToDocument(cls);

            Assert.Equal(before.Layout![methodA.Id], after.Layout![methodA.Id]);
            Assert.Equal(before.Layout![methodB.Id], after.Layout![methodB.Id]);
        }

        // DF-T25: a node whose Name equals its DefaultName omits `name` and reads back unchanged; a
        // renamed node keeps its `name`.
        [Fact]
        public void DefaultNamedNodeOmitsNameAndRenamedNodeKeepsIt()
        {
            var cls = new ClassGraph { Name = "C" };
            var method = new MethodGraph("M") { Class = cls };
            cls.Methods.Add(method);
            var writer = new MethodSpecifier("X", [], [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<object>(), []);
            var defaultNamed = new CallMethodNode(method, writer);
            var renamed = new CallMethodNode(method, writer) { Name = "Greeting" };

            DocumentMapper mapper = NewMapper();
            ClassDocument document = mapper.ToDocument(cls);

            IReadOnlyList<NodeDocument> nodeDocuments = document.Methods!.Single().Graph.Nodes;
            NodeDocument defaultDoc = nodeDocuments.Single(n => n.Id == defaultNamed.Id);
            NodeDocument renamedDoc = nodeDocuments.Single(n => n.Id == renamed.Id);

            Assert.Null(defaultDoc.Name);
            Assert.Equal("Greeting", renamedDoc.Name);

            var issues = new List<DocumentIssue>();
            ClassGraph rebuilt = mapper.FromDocument(document, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));
            List<CallMethodNode> rebuiltCalls = rebuilt.Methods.Single().Nodes.OfType<CallMethodNode>().ToList();

            Assert.Contains(rebuiltCalls, n => n.Name == "CallMethodNode");
            Assert.Contains(rebuiltCalls, n => n.Name == "Greeting");
        }
    }
}
