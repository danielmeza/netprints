using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>
    /// The contract of <see cref="IMemberReferencingNode"/> (R1): <see cref="IMemberReferencingNode.RefersTo"/>
    /// flips once the node is retargeted, the returned undo restores the state, and the retargeted node survives
    /// a serializer round trip. A node kind joins by deriving once.
    /// </summary>
    /// <typeparam name="TNode">The node kind under test.</typeparam>
    public abstract class MemberReferencingNodeContract<TNode>
        where TNode : Node, IMemberReferencingNode
    {
        protected const string NewName = "Renamed";

        protected static ClassGraph NewClass() => new() { Name = "Contract", Namespace = "Contracts", Visibility = MemberVisibility.Public };

        /// <summary>The key of the member the created node points at.</summary>
        protected abstract MemberKey OldKey { get; }

        /// <summary>Creates a node pointing at <see cref="OldKey"/>, added to <paramref name="graph"/>.</summary>
        protected abstract TNode CreateNode(NodeGraph graph);

        /// <summary>The node's own record of its target, as text, to compare before and after.</summary>
        protected abstract string TargetOf(TNode node);

        private TNode NodeInAMethod(out MethodGraph method)
        {
            ClassGraph cls = NewClass();
            method = new MethodGraph("Host") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
            cls.Methods.Add(method);
            return CreateNode(method);
        }

        private MemberKey Renamed => OldKey with { Name = NewName };

        [Fact]
        public void RefersToTheMemberItPointsAt()
        {
            TNode node = NodeInAMethod(out _);

            Assert.True(node.RefersTo(OldKey));
            Assert.False(node.RefersTo(Renamed));
        }

        [Fact]
        public void RefersToIsFalseForAnotherDeclaringType()
        {
            TNode node = NodeInAMethod(out _);

            Assert.False(node.RefersTo(OldKey with { DeclaringType = TypeSpecifier.FromType<object>() }));
        }

        [Fact]
        public void RetargetingFlipsRefersTo()
        {
            TNode node = NodeInAMethod(out _);

            node.Retarget(OldKey, NewName);

            Assert.True(node.RefersTo(Renamed));
            Assert.False(node.RefersTo(OldKey));
        }

        [Fact]
        public void TheUndoRestoresTheState()
        {
            TNode node = NodeInAMethod(out _);
            string before = TargetOf(node);

            Action undo = node.Retarget(OldKey, NewName);

            Assert.NotEqual(before, TargetOf(node));

            undo();

            Assert.Equal(before, TargetOf(node));
            Assert.True(node.RefersTo(OldKey));
        }

        [Fact]
        public async Task ARetargetedNodeSurvivesTheSerializerRoundTrip()
        {
            TNode node = NodeInAMethod(out MethodGraph method);
            node.Retarget(OldKey, NewName);

            ClassGraph loaded = await RoundTripAsync(method.Class ?? throw new InvalidOperationException("The method has no class."));

            TNode reloaded = loaded.Methods.Single().Nodes.OfType<TNode>().Single();
            Assert.True(reloaded.RefersTo(Renamed));
            Assert.Equal(TargetOf(node), TargetOf(reloaded));
        }

        private static async Task<ClassGraph> RoundTripAsync(ClassGraph cls)
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
            var id = new DocumentId("Contract.netpc.json");
            using var stream = new MemoryStream();

            await format.WriteClassAsync(mapper.ToDocument(cls), stream, TestContext.Current.CancellationToken);
            stream.Position = 0;
            ClassDocument document = await format.ReadClassAsync(stream, id, TestContext.Current.CancellationToken);

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = mapper.FromDocument(document, TestProjects.Create("Contracts", "Contracts"), issues, id);
            Assert.Empty(issues);
            return loaded;
        }
    }
}
