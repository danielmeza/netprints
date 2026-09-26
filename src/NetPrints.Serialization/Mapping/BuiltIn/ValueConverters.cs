#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping.BuiltIn;

/// <summary>
/// Converters for the pure value/type-expression node kinds (document-format.md §1.5): a literal, a
/// type reference, an array type, an array value, an explicit cast, a <c>typeof</c> and a type's
/// default value.
/// </summary>
internal static class ValueConverters
{
    /// <summary>Every converter this file contributes, in document-format.md §1.5 order.</summary>
    public static IReadOnlyList<INodeDocumentConverter> All { get; } =
    [
        new LiteralNodeConverter(),
        new TypeNodeConverter(),
        new MakeArrayTypeNodeConverter(),
        new MakeArrayNodeConverter(),
        new ExplicitCastNodeConverter(),
        new TypeOfNodeConverter(),
        new DefaultNodeConverter(),
    ];

    private sealed class LiteralNodeConverter : INodeDocumentConverter
    {
        public string Kind => "literal";
        public Type NodeType => typeof(LiteralNode);
        public Type DocumentType => typeof(LiteralNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var literal = (LiteralNode)node;
            return new LiteralNodeDocument(literal.Id, null, null, context.ToRef(literal.LiteralType));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (LiteralNodeDocument)document;
            return new LiteralNode(graph, (TypeSpecifier)context.FromRef(doc.LiteralType));
        }
    }

    private sealed class TypeNodeConverter : INodeDocumentConverter
    {
        public string Kind => "type";
        public Type NodeType => typeof(TypeNode);
        public Type DocumentType => typeof(TypeNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var typeNode = (TypeNode)node;
            return new TypeNodeDocument(typeNode.Id, null, null, context.ToRef(typeNode.Type));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (TypeNodeDocument)document;
            return new TypeNode(graph, context.FromRef(doc.Type));
        }
    }

    private sealed class MakeArrayTypeNodeConverter : INodeDocumentConverter
    {
        public string Kind => "makeArrayType";
        public Type NodeType => typeof(MakeArrayTypeNode);
        public Type DocumentType => typeof(MakeArrayTypeNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new MakeArrayTypeNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new MakeArrayTypeNode(graph);
    }

    private sealed class MakeArrayNodeConverter : INodeDocumentConverter
    {
        public string Kind => "makeArray";
        public Type NodeType => typeof(MakeArrayNode);
        public Type DocumentType => typeof(MakeArrayNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var array = (MakeArrayNode)node;
            int elementCount = array.UsePredefinedSize ? 0 : array.InputDataPins.Count;
            return new MakeArrayNodeDocument(array.Id, null, null, array.UsePredefinedSize, elementCount);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (MakeArrayNodeDocument)document;
            var array = new MakeArrayNode(graph);

            if (doc.UsePredefinedSize)
            {
                array.UsePredefinedSize = true;
            }
            else
            {
                for (int i = 0; i < doc.ElementCount; i++)
                {
                    array.AddElementPin();
                }
            }

            return array;
        }
    }

    private sealed class ExplicitCastNodeConverter : INodeDocumentConverter
    {
        public string Kind => "explicitCast";
        public Type NodeType => typeof(ExplicitCastNode);
        public Type DocumentType => typeof(ExplicitCastNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new ExplicitCastNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new ExplicitCastNode(graph);
    }

    private sealed class TypeOfNodeConverter : INodeDocumentConverter
    {
        public string Kind => "typeOf";
        public Type NodeType => typeof(TypeOfNode);
        public Type DocumentType => typeof(TypeOfNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new TypeOfNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new TypeOfNode(graph);
    }

    private sealed class DefaultNodeConverter : INodeDocumentConverter
    {
        public string Kind => "default";
        public Type NodeType => typeof(DefaultNode);
        public Type DocumentType => typeof(DefaultNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new DefaultNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new DefaultNode(graph);
    }
}
