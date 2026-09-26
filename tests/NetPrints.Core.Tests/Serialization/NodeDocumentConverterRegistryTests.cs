using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary><see cref="NodeDocumentConverterRegistry"/>'s construction-time validation and lookups.</summary>
    public class NodeDocumentConverterRegistryTests
    {
        private sealed class FakeConverter : INodeDocumentConverter
        {
            public FakeConverter(string kind, Type nodeType) { Kind = kind; NodeType = nodeType; }
            public string Kind { get; }
            public Type NodeType { get; }
            public Type DocumentType => typeof(TypeReturnNodeDocument);
            public NodeDocument ToDocument(Node node, NodeMappingContext context) => throw new NotSupportedException();
            public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => throw new NotSupportedException();
        }

        private static readonly IReadOnlyList<IJsonTypeInfoResolver> NoResolvers = [];

        [Fact]
        public void BuiltInKindWithoutSlashIsAccepted()
        {
            var registry = new NodeDocumentConverterRegistry([new FakeConverter("literal", typeof(LiteralNode))], NoResolvers);
            Assert.NotNull(registry.FindByKind("literal"));
        }

        [Fact]
        public void ExtensionKindWithSlashIsAccepted()
        {
            var registry = new NodeDocumentConverterRegistry([new FakeConverter("test.ext/foo", typeof(LiteralNode))], NoResolvers);
            Assert.NotNull(registry.FindByKind("test.ext/foo"));
        }

        [Fact]
        public void UnknownKindWithoutSlashIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new NodeDocumentConverterRegistry([new FakeConverter("bogusKind", typeof(LiteralNode))], NoResolvers));
        }

        [Fact]
        public void DuplicateKindIsRejected()
        {
            var converters = new INodeDocumentConverter[]
            {
                new FakeConverter("literal", typeof(LiteralNode)),
                new FakeConverter("literal", typeof(TypeNode)),
            };

            Assert.Throws<ArgumentException>(() => new NodeDocumentConverterRegistry(converters, NoResolvers));
        }

        [Fact]
        public void DuplicateNodeTypeIsRejected()
        {
            var converters = new INodeDocumentConverter[]
            {
                new FakeConverter("literal", typeof(LiteralNode)),
                new FakeConverter("type", typeof(LiteralNode)),
            };

            Assert.Throws<ArgumentException>(() => new NodeDocumentConverterRegistry(converters, NoResolvers));
        }

        [Fact]
        public void FindByKindAndNodeTypeReturnNullForUnknowns()
        {
            var registry = new NodeDocumentConverterRegistry([new FakeConverter("literal", typeof(LiteralNode))], NoResolvers);

            Assert.Null(registry.FindByKind("type"));
            Assert.Null(registry.FindByNodeType(typeof(TypeNode)));
        }

        [Fact]
        public void BuiltInContainsExactlyTwentyThreeConvertersSoFar()
        {
            // eventEntry (the 24th built-in kind of document-format.md §1.5) is added in sub-phase G
            // (T080); until then NodeDocumentConverterRegistry.BuiltIn covers the other 23.
            Assert.Equal(23, NodeDocumentConverterRegistry.BuiltIn.Count);

            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, NoResolvers);
            Assert.Equal(23, registry.Converters.Count);
        }
    }
}
