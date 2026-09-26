#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping.BuiltIn;

/// <summary>
/// Converters for the control-flow node kinds that take no kind-specific fields beyond a reroute's
/// pin shape (document-format.md §1.5): if/else, a for-loop, a ternary, <c>await</c>, <c>throw</c>,
/// and a layout-only reroute.
/// </summary>
internal static class FlowConverters
{
    /// <summary>Every converter this file contributes, in document-format.md §1.5 order.</summary>
    public static IReadOnlyList<INodeDocumentConverter> All { get; } =
    [
        new IfElseNodeConverter(),
        new ForLoopNodeConverter(),
        new TernaryNodeConverter(),
        new AwaitNodeConverter(),
        new ThrowNodeConverter(),
        new RerouteNodeConverter(),
    ];

    private sealed class IfElseNodeConverter : INodeDocumentConverter
    {
        public string Kind => "ifElse";
        public Type NodeType => typeof(IfElseNode);
        public Type DocumentType => typeof(IfElseNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new IfElseNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new IfElseNode(graph);
    }

    private sealed class ForLoopNodeConverter : INodeDocumentConverter
    {
        public string Kind => "forLoop";
        public Type NodeType => typeof(ForLoopNode);
        public Type DocumentType => typeof(ForLoopNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new ForLoopNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new ForLoopNode(graph);
    }

    private sealed class TernaryNodeConverter : INodeDocumentConverter
    {
        public string Kind => "ternary";
        public Type NodeType => typeof(TernaryNode);
        public Type DocumentType => typeof(TernaryNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new TernaryNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new TernaryNode(graph);
    }

    private sealed class AwaitNodeConverter : INodeDocumentConverter
    {
        public string Kind => "await";
        public Type NodeType => typeof(AwaitNode);
        public Type DocumentType => typeof(AwaitNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new AwaitNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new AwaitNode(graph);
    }

    private sealed class ThrowNodeConverter : INodeDocumentConverter
    {
        public string Kind => "throw";
        public Type NodeType => typeof(ThrowNode);
        public Type DocumentType => typeof(ThrowNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context) => new ThrowNodeDocument(node.Id, null, null);

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new ThrowNode(graph);
    }

    private sealed class RerouteNodeConverter : INodeDocumentConverter
    {
        public string Kind => "reroute";
        public Type NodeType => typeof(RerouteNode);
        public Type DocumentType => typeof(RerouteNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var reroute = (RerouteNode)node;

            if (reroute.DataRerouteCount > 0)
            {
                var dataTypes = new List<TypeRef[]>();
                for (int i = 0; i < reroute.DataRerouteCount; i++)
                {
                    BaseType inputType = reroute.InputDataPins[i].PinType.Value ?? TypeSpecifier.FromType<object>();
                    BaseType outputType = reroute.OutputDataPins[i].PinType.Value ?? TypeSpecifier.FromType<object>();
                    dataTypes.Add([context.ToRef(inputType), context.ToRef(outputType)]);
                }

                return new RerouteNodeDocument(reroute.Id, null, null, "data", 0, dataTypes);
            }

            if (reroute.TypeRerouteCount > 0)
            {
                return new RerouteNodeDocument(reroute.Id, null, null, "type", reroute.TypeRerouteCount, null);
            }

            // ExecRerouteCount, or a degenerate zero-pin reroute (not produced by any factory method
            // with a positive count, but not forbidden either): "exec" with the actual count (0 or more).
            return new RerouteNodeDocument(reroute.Id, null, null, "exec", reroute.ExecRerouteCount, null);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (RerouteNodeDocument)document;

            return doc.PinKind switch
            {
                "exec" => RerouteNode.MakeExecution(graph, doc.Count),
                "type" => RerouteNode.MakeType(graph, doc.Count),
                "data" => RerouteNode.MakeData(graph, (doc.DataTypes ?? [])
                    .Select(pair => Tuple.Create(context.FromRef(pair[0]), context.FromRef(pair[1])))),
                _ => throw new DocumentFormatException($"Unknown reroute pin kind '{doc.PinKind}'."),
            };
        }
    }
}
