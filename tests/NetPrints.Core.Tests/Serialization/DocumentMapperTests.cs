using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
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
            new(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []), NullLogger<DocumentMapper>.Instance);

        private static readonly JsonSerializerOptions JsonOptions =
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])).SerializerOptions;

        private static GraphDocument SimpleMethodGraph(string entryId, string returnId) =>
            new([new MethodEntryNodeDocument(entryId, null, null, 0, null), new ReturnNodeDocument(returnId, null, null, 0)], null, null);

        // T054b (research.md R21): FromDocument is strict now, so every handcrafted document it reads
        // needs IdFormat-shaped ids or the mapper repairs them out from under the test (NPD009).
        private static string NodeId(long value) => IdFormat.Format('n', value);
        private static string MemberId(long value) => IdFormat.Format('m', value);

        // DF-T06: every built-in kind round-trips, including dynamic pin counts and renamed pins.
        [Fact]
        public void AllNodesRoundTripsThroughToDocumentAndFromDocument()
        {
            using var _ = IdGeneration.Use(new SeededIdGenerator(42));
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.csproj");
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
            Project project = AllNodesFixtureFactory.CreateAllNodes("AllNodes.csproj");
            ClassGraph original = project.Classes.Single();
            TypeSpecifier originalItemsType = original.Variables.Single().Type;

            DocumentMapper mapper = NewMapper();
            ClassDocument document = mapper.ToDocument(original);

            var issues = new List<DocumentIssue>();
            ClassGraph rebuilt = mapper.FromDocument(document, project, issues, new DocumentId("AllNodes.Everything.netpc.json"));

            Assert.All(rebuilt.Variables, v => Assert.NotNull(v.TypeGraph));
            Assert.Equal(originalItemsType, rebuilt.Variables.Single().Type);
        }

        // DF-T08 (document part): an unknown node kind and a connection into it are preserved, including
        // its position and its place in the node order (R1-03: a preserved node used to move to the end
        // of `nodes` and lose its `layout` entry to a false NPD004 on every re-save).
        [Fact]
        public void UnknownNodeAndItsConnectionArePreservedRoundTripWithLayoutAndOrder()
        {
            // "n9" (the unknown node) is exempt from strict-id repair: it is opaque, preserved state,
            // never mapped to a real Node. It comes first in both `nodes` and `layout`, ahead of the
            // known node "n0" — the reviewer's repro order (R1-03).
            JsonElement raw = JsonDocument.Parse("""{"$kind":"test.ext/widget","id":"n9","extra":1}""").RootElement;
            var unknownNode = new UnknownNodeDocument("n9", "test.ext/widget", raw);
            var classReturn = new ClassReturnNodeDocument(NodeId(0), null, null, 0);
            var classGraph = new GraphDocument([unknownNode, classReturn],
                [new ConnectionDocument($"n9/out.type.Whatever", $"{NodeId(0)}/in.type.BaseType")], null);
            var layout = new SortedDictionary<string, SortedDictionary<string, int[]>>
            {
                ["class"] = new SortedDictionary<string, int[]> { ["n9"] = [300, 400], [NodeId(0)] = [10, 20] },
            };
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, layout);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Contains(issues, i => i.Code == DocumentIssue.UnknownNodeKind);
            Assert.DoesNotContain(issues, i => i.Code == DocumentIssue.LayoutEntryIgnored);
            Assert.NotNull(cls.PreservedDocumentState);
            Assert.Equal(10, cls.Nodes.Single().PositionX);
            Assert.Equal(20, cls.Nodes.Single().PositionY);

            ClassDocument roundTripped = mapper.ToDocument(cls);

            // Node order: the preserved node is back at its original index, not appended at the end.
            Assert.Equal(["n9", NodeId(0)], roundTripped.ClassGraph.Nodes.Select(n => n.Id));
            var preservedNode = Assert.IsType<UnknownNodeDocument>(roundTripped.ClassGraph.Nodes[0]);
            Assert.Equal("test.ext/widget", preservedNode.Kind);
            ConnectionDocument preservedConnection = Assert.Single(roundTripped.ClassGraph.Connections!);
            Assert.Equal("n9/out.type.Whatever", preservedConnection.From);
            Assert.Equal($"{NodeId(0)}/in.type.BaseType", preservedConnection.To);

            // Layout: both positions survive, including the preserved node's.
            SortedDictionary<string, int[]> roundTrippedLayout = Assert.Single(roundTripped.Layout!).Value;
            Assert.Equal(new[] { 300, 400 }, roundTrippedLayout["n9"]);
            Assert.Equal(new[] { 10, 20 }, roundTrippedLayout[NodeId(0)]);
        }

        // R1-04: a pure-capable node's purity round-trips through the document. Before the fix, a pure
        // node had no way to record that and reloaded impure, with its exec pins back.
        [Fact]
        public void PureExplicitCastNodeRoundTripsWithoutExecPins()
        {
            var cast = new ExplicitCastNodeDocument(NodeId(1), null, null, Pure: true);
            var classGraph = new GraphDocument([new ClassReturnNodeDocument(NodeId(0), null, null, 0), cast], null, null);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            Node castNode = cls.Nodes.Single(n => n.Id == NodeId(1));
            Assert.True(castNode.IsPure);
            Assert.Empty(castNode.InputExecPins);
            Assert.Empty(castNode.OutputExecPins);

            ClassDocument roundTripped = mapper.ToDocument(cls);
            var roundTrippedCast = Assert.IsType<ExplicitCastNodeDocument>(roundTripped.ClassGraph.Nodes.Single(n => n.Id == NodeId(1)));
            Assert.True(roundTrippedCast.Pure);
        }

        // R1-05: a generic type reference where a concrete type is required (a literal's type) must fail
        // as DocumentFormatException, not an unchecked InvalidCastException from the old blind cast.
        [Fact]
        public void GenericTypeRefWhereConcreteTypeIsRequiredThrowsDocumentFormatException()
        {
            var literal = new LiteralNodeDocument(NodeId(1), null, null, new TypeRef("T", Generic: true));
            var classGraph = new GraphDocument([new ClassReturnNodeDocument(NodeId(0), null, null, 0), literal], null, null);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                classGraph, null, null, null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            Assert.Throws<DocumentFormatException>(() =>
                mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json")));
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
            var entry = new MethodEntryNodeDocument(NodeId(3), null, null, 0, null);
            var call = new CallMethodNodeDocument(NodeId(2), null, null, editedMethod, 0, Pure: false);
            var literalA = new LiteralNodeDocument(NodeId(6), null, null, intRef);
            var literalB = new LiteralNodeDocument(NodeId(7), null, null, intRef);

            var connections = new List<ConnectionDocument>
            {
                new($"{NodeId(6)}/out.data.Value", $"{NodeId(2)}/in.data.a"),
                new($"{NodeId(7)}/out.data.Value", $"{NodeId(2)}/in.data.b"),
            };

            var methodGraph = new GraphDocument(
                [entry, new ReturnNodeDocument(NodeId(4), null, null, 0), call, literalA, literalB],
                connections, null);
            var methodDocument = new MethodDocument(MemberId(1), "Caller", MemberVisibility.Public, MethodModifiers.None, methodGraph);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(5), null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

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
            var literal = new LiteralNodeDocument(NodeId(2), null, null, new TypeRef("System.Int32"));
            var methodGraph = new GraphDocument(
                [new MethodEntryNodeDocument(NodeId(0), null, null, 0, null), new ReturnNodeDocument(NodeId(1), null, null, 0), literal],
                [new ConnectionDocument($"{NodeId(0)}/out.exec.Exec", $"{NodeId(1)}/in.exec.Exec")], null);
            var methodDocument = new MethodDocument(MemberId(1), "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);

            var layout = new SortedDictionary<string, SortedDictionary<string, int[]>>(System.StringComparer.Ordinal)
            {
                [MemberId(1)] = new SortedDictionary<string, int[]>(System.StringComparer.Ordinal)
                {
                    [NodeId(0)] = [100, 50],
                    [NodeId(1)] = [400, 50],
                    // The literal intentionally has no entry: it must be auto-placed.
                },
            };

            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(3), null, null, 0)], null, null),
                null, [methodDocument], null, null, layout);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            MethodGraph method = cls.Methods.Single();
            Node n0 = method.FindNode(NodeId(0))!;
            Node n1 = method.FindNode(NodeId(1))!;
            Node literalNode = method.Nodes.OfType<LiteralNode>().Single();

            Assert.Equal(100, n0.PositionX);
            Assert.Equal(50, n0.PositionY);
            Assert.Equal(400, n1.PositionX);
            Assert.Equal(50, n1.PositionY);

            var issues2 = new List<DocumentIssue>();
            ClassGraph cls2 = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues2, new DocumentId("C.netpc.json"));
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
                classDocument, TestProjects.Create("P", "P"), new List<DocumentIssue>(), new DocumentId("C.netpc.json")));
        }

        // DF-T22 (document part): a duplicate member id does not fail the load (merge safety); the
        // later duplicate is reassigned a fresh id and reported as a DuplicateIdReassigned warning.
        [Fact]
        public void DuplicateMemberIdIsReassignedAndReported()
        {
            var methodA = new MethodDocument(MemberId(1), "A", MemberVisibility.Public, MethodModifiers.None, SimpleMethodGraph(NodeId(0), NodeId(1)));
            var methodB = new MethodDocument(MemberId(1), "B", MemberVisibility.Public, MethodModifiers.None, SimpleMethodGraph(NodeId(2), NodeId(3)));
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(4), null, null, 0)], null, null),
                null, [methodA, methodB], null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Contains(issues, i => i.Code == DocumentIssue.DuplicateIdReassigned && i.Severity == DocumentIssueSeverity.Warning);

            MethodGraph first = cls.Methods.Single(m => m.Name == "A");
            MethodGraph second = cls.Methods.Single(m => m.Name == "B");
            Assert.Equal(MemberId(1), first.Id);
            Assert.NotEqual(first.Id, second.Id);
        }

        // DF-T18 (document part): a duplicate node id within a graph does not fail the load; the later
        // duplicate is reassigned a fresh id and reported as a DuplicateIdReassigned warning, indexed
        // consistently (FindNode resolves the id it now actually holds).
        [Fact]
        public void DuplicateNodeIdIsReassignedAndReported()
        {
            var entry = new MethodEntryNodeDocument(NodeId(0), null, null, 0, null);
            var literalA = new LiteralNodeDocument(NodeId(1), null, null, new TypeRef("System.Int32"));
            var literalB = new LiteralNodeDocument(NodeId(1), null, null, new TypeRef("System.Int32"));
            var methodGraph = new GraphDocument([entry, new ReturnNodeDocument(NodeId(2), null, null, 0), literalA, literalB], null, null);
            var methodDocument = new MethodDocument(MemberId(1), "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(3), null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Contains(issues, i => i.Code == DocumentIssue.DuplicateIdReassigned && i.Severity == DocumentIssueSeverity.Warning);

            MethodGraph method = cls.Methods.Single();
            List<LiteralNode> literals = method.Nodes.OfType<LiteralNode>().ToList();
            Assert.Equal(2, literals.Count);
            Assert.NotEqual(literals[0].Id, literals[1].Id);
            Assert.Contains(literals, l => l.Id == NodeId(1));
            Assert.Same(literals.Single(l => l.Id == NodeId(1)), method.FindNode(NodeId(1)));
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
            ClassGraph rebuilt = mapper.FromDocument(document, TestProjects.Create("P", "P"), issues, new DocumentId("C.netpc.json"));
            List<CallMethodNode> rebuiltCalls = rebuilt.Methods.Single().Nodes.OfType<CallMethodNode>().ToList();

            Assert.Contains(rebuiltCalls, n => n.Name == "CallMethodNode");
            Assert.Contains(rebuiltCalls, n => n.Name == "Greeting");
        }
    }
}
