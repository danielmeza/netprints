using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>DF-T18, node part: <see cref="Node.Id"/> allocation, uniqueness and legacy import.</summary>
    public class NodeIdTests
    {
        private sealed class QueueIdGenerator : IIdGenerator
        {
            private readonly Queue<string> ids;

            public QueueIdGenerator(params string[] ids)
            {
                this.ids = new Queue<string>(ids);
            }

            public string NewId(char prefix) => ids.Dequeue();
        }

        [Fact]
        public void NewNodesGetUniqueRandomIdsMatchingTheAlphabet()
        {
            var regex = new Regex("^n[0-9a-hjkmnp-tv-z]{6}$");
            var graph = new MethodGraph("Main");

            for (int i = 0; i < 50; i++)
            {
                var node = new LiteralNode(graph, TypeSpecifier.FromType<int>());
                Assert.Matches(regex, node.Id);
            }

            var ids = graph.Nodes.Select(n => n.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void AllocateNodeIdRetriesAnAlreadyUsedId()
        {
            var graph = new MethodGraph("Main");
            string used = graph.EntryNode.Id;

            using (IdGeneration.Use(new QueueIdGenerator(used, "nabcdef")))
            {
                var literal = new LiteralNode(graph, TypeSpecifier.FromType<int>());
                Assert.Equal("nabcdef", literal.Id);
            }
        }

        private static List<string> NodeIdsWithSeed(int seed)
        {
            using var scope = IdGeneration.Use(new SeededIdGenerator(seed));
            var graph = new MethodGraph("Main");
            new LiteralNode(graph, TypeSpecifier.FromType<int>());
            new LiteralNode(graph, TypeSpecifier.FromType<string>());
            return graph.Nodes.Select(n => n.Id).ToList();
        }

        [Fact]
        public void SeededGeneratorGivesTheSameIdsOnEveryRun()
        {
            Assert.Equal(NodeIdsWithSeed(42), NodeIdsWithSeed(42));
        }

        [Fact]
        public void AssignLegacyNodeIdsGivesPositionalIds()
        {
            var graph = new MethodGraph("Main");
            graph.Nodes.Clear();

            var first = (LiteralNode)RuntimeHelpers.GetUninitializedObject(typeof(LiteralNode));
            var second = (LiteralNode)RuntimeHelpers.GetUninitializedObject(typeof(LiteralNode));
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);

            graph.AssignLegacyNodeIds();

            Assert.Equal("n0", first.Id);
            Assert.Equal("n1", second.Id);
        }

        [Fact]
        public void AssignLegacyNodeIdsThrowsWhenAnyIdIsAlreadySet()
        {
            var graph = new MethodGraph("Main");

            Assert.Throws<InvalidOperationException>(() => graph.AssignLegacyNodeIds());
        }

        [Fact]
        public void RemovingAndReAddingANodeKeepsItsId()
        {
            var graph = new MethodGraph("Main");
            var literal = new LiteralNode(graph, TypeSpecifier.FromType<int>());
            string id = literal.Id;

            graph.Nodes.Remove(literal);
            graph.Nodes.Add(literal);

            Assert.Equal(id, literal.Id);
            Assert.Same(literal, graph.FindNode(id));
        }

        [Fact]
        public void FindNodeReturnsNullForAnUnknownId()
        {
            var graph = new MethodGraph("Main");

            Assert.Null(graph.FindNode("n000000"));
        }
    }
}
