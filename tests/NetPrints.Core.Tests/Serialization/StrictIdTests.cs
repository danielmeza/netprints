using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    /// <summary>
    /// DF-T28 (document-format.md §1.4.1, §2.6, research.md R21): a node or member id not matching
    /// <see cref="IdFormat"/> is replaced by a fresh one, every reference to the old text follows, and
    /// the repair is reported as <see cref="DocumentIssue.InvalidIdReassigned"/> (<c>NPD009</c>).
    /// </summary>
    public class StrictIdTests
    {
        private static DocumentMapper NewMapper() =>
            new(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []));

        private static string NodeId(long value) => IdFormat.Format('n', value);
        private static string MemberId(long value) => IdFormat.Format('m', value);

        [Fact]
        public void InvalidNodeAndMemberIdsAreReassignedAndReferencesFollow()
        {
            var classReturn = new ClassReturnNodeDocument("n0", null, null, 0);
            var entry = new MethodEntryNodeDocument("start", null, null, 0, null);
            var ret = new ReturnNodeDocument("N00000000057K3", null, null, 0);
            var methodGraph = new GraphDocument(
                [entry, ret],
                [new ConnectionDocument("start/out.exec.Exec", "N00000000057K3/in.exec.Exec")],
                null);
            var methodDocument = new MethodDocument("m1", "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);

            var layout = new SortedDictionary<string, SortedDictionary<string, int[]>>(StringComparer.Ordinal)
            {
                ["m1"] = new SortedDictionary<string, int[]>(StringComparer.Ordinal)
                {
                    ["start"] = [100, 50],
                    ["N00000000057K3"] = [400, 50],
                },
                ["class"] = new SortedDictionary<string, int[]>(StringComparer.Ordinal)
                {
                    ["n0"] = [10, 10],
                },
            };

            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([classReturn], null, null),
                null, [methodDocument], null, null, layout);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));

            // One NPD009 per replaced id ("n0", "start", "N00000000057K3", "m1"); nothing else went wrong.
            Assert.Equal(4, issues.Count);
            Assert.All(issues, issue => Assert.Equal(DocumentIssue.InvalidIdReassigned, issue.Code));
            Assert.Contains(issues, issue => issue.Message.Contains("'n0'", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Message.Contains("'start'", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Message.Contains("'N00000000057K3'", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Message.Contains("'m1'", StringComparison.Ordinal));

            MethodGraph method = cls.Methods.Single();
            var entryNode = method.Nodes.OfType<MethodEntryNode>().Single();
            var returnNode = method.Nodes.OfType<ReturnNode>().Single();

            Assert.Matches(IdFormat.Pattern, cls.ReturnNode.Id);
            Assert.Matches(IdFormat.Pattern, method.Id);
            Assert.Matches(IdFormat.Pattern, entryNode.Id);
            Assert.Matches(IdFormat.Pattern, returnNode.Id);

            // The connection followed the rename (no NPD002).
            NodeInputExecPin? incoming = entryNode.InitialExecutionPin.OutgoingPin;
            Assert.NotNull(incoming);
            Assert.Same(returnNode, incoming.Node);

            // The positions followed the rename too (no NPD004, no auto-placement).
            Assert.Equal(100, entryNode.PositionX);
            Assert.Equal(50, entryNode.PositionY);
            Assert.Equal(400, returnNode.PositionX);
            Assert.Equal(50, returnNode.PositionY);
            Assert.Equal(10, cls.ReturnNode.PositionX);
            Assert.Equal(10, cls.ReturnNode.PositionY);
        }

        /// <summary>Returns the same fixed id for its first <paramref name="repeatCount"/> calls
        /// (regardless of prefix), so two independent invalid-shape repairs can be made to collide on
        /// purpose — every node construction along the way allocates its own (later overwritten and
        /// discarded) placeholder id too, so this covers those as well as the repairs themselves; falls
        /// back to a real generator once exhausted (the collision's own repair needs a real,
        /// non-colliding id, and a generous <paramref name="repeatCount"/> means this is reached only
        /// after the collision is already forced).</summary>
        private sealed class FixedThenFallbackIdGenerator : IIdGenerator
        {
            private readonly string fixedId;
            private readonly IIdGenerator fallback = new SeededIdGenerator(4200);
            private int remaining;

            public FixedThenFallbackIdGenerator(string fixedId, int repeatCount)
            {
                this.fixedId = fixedId;
                remaining = repeatCount;
            }

            public string NewId(char prefix)
            {
                if (remaining > 0)
                {
                    remaining--;
                    return fixedId;
                }

                return fallback.NewId(prefix);
            }
        }

        [Fact]
        public void InvalidIdRepeatedTwiceEndsWithTwoDistinctIdsAndAlsoReportsADuplicate()
        {
            // Node ids are unique per graph (data-model.md §2), so both invalid occurrences of the same
            // raw text "n0" need to be in the same graph for the second one's repair to also collide.
            // The entry and return nodes get real, valid ids so MethodGraph's own auto-created fixed
            // nodes (which allocate a placeholder id before this loop even starts) stay out of the way.
            var entry = new MethodEntryNodeDocument(NodeId(0), null, null, 0, null);
            var ret = new ReturnNodeDocument(NodeId(1), null, null, 0);
            var literalA = new LiteralNodeDocument("n0", null, null, new TypeRef("System.Int32"));
            var literalB = new LiteralNodeDocument("n0", null, null, new TypeRef("System.Int32")); // same invalid raw text as literalA
            var methodGraph = new GraphDocument([entry, ret, literalA, literalB], null, null);
            var methodDocument = new MethodDocument(MemberId(1), "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);

            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(3), null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            var issues = new List<DocumentIssue>();

            // Every repair (and every node construction before it) is forced to the exact same
            // replacement id, so literalB's repair collides with literalA's.
            string forcedReplacement = NodeId(9);
            using var _ = IdGeneration.Use(new FixedThenFallbackIdGenerator(forcedReplacement, repeatCount: 20));
            ClassGraph cls = mapper.FromDocument(classDocument, Project.CreateNew("P", "P"), issues, new DocumentId("C.netpc.json"));

            Assert.Equal(2, issues.Count(issue => issue.Code == DocumentIssue.InvalidIdReassigned));
            Assert.Equal(1, issues.Count(issue => issue.Code == DocumentIssue.DuplicateIdReassigned));

            MethodGraph method = cls.Methods.Single();
            List<LiteralNode> literals = method.Nodes.OfType<LiteralNode>().ToList();
            Assert.Equal(2, literals.Count);
            Assert.NotEqual(literals[0].Id, literals[1].Id);
            Assert.Matches(IdFormat.Pattern, literals[0].Id);
            Assert.Matches(IdFormat.Pattern, literals[1].Id);
        }

        [Fact]
        public void MissingNodeIdStillThrows()
        {
            var entry = new MethodEntryNodeDocument(string.Empty, null, null, 0, null);
            var methodGraph = new GraphDocument([entry, new ReturnNodeDocument(NodeId(1), null, null, 0)], null, null);
            var methodDocument = new MethodDocument(MemberId(2), "M", MemberVisibility.Public, MethodModifiers.None, methodGraph);
            var classDocument = new ClassDocument(1, null, "C", MemberVisibility.Public, ClassModifiers.None, null,
                new GraphDocument([new ClassReturnNodeDocument(NodeId(3), null, null, 0)], null, null),
                null, [methodDocument], null, null, null);

            DocumentMapper mapper = NewMapper();
            Assert.Throws<DocumentFormatException>(() => mapper.FromDocument(
                classDocument, Project.CreateNew("P", "P"), new List<DocumentIssue>(), new DocumentId("C.netpc.json")));
        }

        // The migrated fixtures (T054a) and the committed sample are already IdFormat-shaped: loading
        // them reports no issues at all, in particular no NPD009.
        [Theory]
        [MemberData(nameof(GoldenCSharpTests.Fixtures), MemberType = typeof(GoldenCSharpTests))]
        public async Task MigratedFixturesLoadWithNoInvalidIdIssues(string fixtureName, string classFileName)
        {
            string path = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", fixtureName, classFileName);
            await AssertLoadsWithNoIssuesAsync(path, fixtureName);
        }

        [Fact]
        public async Task SampleLoadsWithNoInvalidIdIssues()
        {
            string path = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld", "HelloWorld.Program.netpc.json");
            await AssertLoadsWithNoIssuesAsync(path, "HelloWorld");
        }

        private static async Task AssertLoadsWithNoIssuesAsync(string path, string projectName)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]));
            var id = new DocumentId(Path.GetFileName(path));

            ClassDocument document;
            using (FileStream stream = File.OpenRead(path))
            {
                document = await format.ReadClassAsync(stream, id, TestContext.Current.CancellationToken);
            }

            var mapper = new DocumentMapper(registry);
            var issues = new List<DocumentIssue>();
            mapper.FromDocument(document, Project.CreateNew(projectName, projectName), issues, id);

            Assert.Empty(issues);
        }
    }
}
