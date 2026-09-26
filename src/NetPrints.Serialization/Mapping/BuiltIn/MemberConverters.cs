#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping.BuiltIn;

/// <summary>
/// Converters for the node kinds that call or reference another member: a method call, a constructor
/// call, a delegate creation, and a variable getter/setter (document-format.md §1.5). Each node's
/// constructor builds its whole pin set from the referenced member's specifier, so <c>CreateNode</c> is
/// just <see langword="new"/> with the mapped reference.
/// </summary>
internal static class MemberConverters
{
    /// <summary>Every converter this file contributes, in document-format.md §1.5 order.</summary>
    public static IReadOnlyList<INodeDocumentConverter> All { get; } =
    [
        new CallMethodNodeConverter(),
        new ConstructorNodeConverter(),
        new MakeDelegateNodeConverter(),
        new VariableGetterNodeConverter(),
        new VariableSetterNodeConverter(),
    ];

    private sealed class CallMethodNodeConverter : INodeDocumentConverter
    {
        public string Kind => "callMethod";
        public Type NodeType => typeof(CallMethodNode);
        public Type DocumentType => typeof(CallMethodNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var call = (CallMethodNode)node;
            return new CallMethodNodeDocument(call.Id, null, null, context.ToRef(call.MethodSpecifier), call.InputTypePins.Count);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (CallMethodNodeDocument)document;
            // The constructor derives every pin (target, exec/catch, arguments, returns, generic
            // argument type pins) from the method specifier; doc.GenericArgumentCount is redundant
            // with method.genericArgs.Count and needs no separate action.
            return new CallMethodNode(graph, context.FromRef(doc.Method));
        }
    }

    private sealed class ConstructorNodeConverter : INodeDocumentConverter
    {
        public string Kind => "constructor";
        public Type NodeType => typeof(ConstructorNode);
        public Type DocumentType => typeof(ConstructorNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var ctor = (ConstructorNode)node;
            return new ConstructorNodeDocument(ctor.Id, null, null, context.ToRef(ctor.ConstructorSpecifier));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (ConstructorNodeDocument)document;
            return new ConstructorNode(graph, context.FromRef(doc.Constructor));
        }
    }

    private sealed class MakeDelegateNodeConverter : INodeDocumentConverter
    {
        public string Kind => "makeDelegate";
        public Type NodeType => typeof(MakeDelegateNode);
        public Type DocumentType => typeof(MakeDelegateNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var makeDelegate = (MakeDelegateNode)node;
            return new MakeDelegateNodeDocument(makeDelegate.Id, null, null, context.ToRef(makeDelegate.MethodSpecifier));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (MakeDelegateNodeDocument)document;
            return new MakeDelegateNode(graph, context.FromRef(doc.Method));
        }
    }

    private sealed class VariableGetterNodeConverter : INodeDocumentConverter
    {
        public string Kind => "variableGetter";
        public Type NodeType => typeof(VariableGetterNode);
        public Type DocumentType => typeof(VariableGetterNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var getter = (VariableGetterNode)node;
            return new VariableGetterNodeDocument(getter.Id, null, null, context.ToRef(getter.Variable));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (VariableGetterNodeDocument)document;
            return new VariableGetterNode(graph, context.FromRef(doc.Variable));
        }
    }

    private sealed class VariableSetterNodeConverter : INodeDocumentConverter
    {
        public string Kind => "variableSetter";
        public Type NodeType => typeof(VariableSetterNode);
        public Type DocumentType => typeof(VariableSetterNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var setter = (VariableSetterNode)node;
            return new VariableSetterNodeDocument(setter.Id, null, null, context.ToRef(setter.Variable));
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (VariableSetterNodeDocument)document;
            return new VariableSetterNode(graph, context.FromRef(doc.Variable));
        }
    }
}
