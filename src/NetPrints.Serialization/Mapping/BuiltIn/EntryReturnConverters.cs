#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping.BuiltIn;

/// <summary>
/// Converters for the five "fixed" node kinds every graph structurally has exactly one of (a class,
/// type, method or constructor graph's own entry/return node), plus a method graph's additional return
/// nodes (document-format.md §1.5). Unlike every other built-in converter, these never construct their
/// node with <see langword="new"/> for the graph's one fixed node: the graph's own constructor already
/// did that (data-model.md §1), so <see cref="INodeDocumentConverter.CreateNode"/> fetches and
/// reconfigures the existing instance instead. <c>Id</c>/<c>Name</c>/<c>Pins</c> on the returned
/// document are placeholders; the mapper (<c>Mapping/DocumentMapper.cs</c>) overwrites them.
/// </summary>
internal static class EntryReturnConverters
{
    /// <summary>Every converter this file contributes, in document-format.md §1.5 order.</summary>
    public static IReadOnlyList<INodeDocumentConverter> All { get; } =
    [
        new MethodEntryNodeConverter(),
        new ConstructorEntryNodeConverter(),
        new ReturnNodeConverter(),
        new ClassReturnNodeConverter(),
        new TypeReturnNodeConverter(),
    ];

    private sealed class MethodEntryNodeConverter : INodeDocumentConverter
    {
        public string Kind => "methodEntry";
        public Type NodeType => typeof(MethodEntryNode);
        public Type DocumentType => typeof(MethodEntryNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var entry = (MethodEntryNode)node;
            IReadOnlyList<string>? genericArguments = entry.OutputTypePins.Count > 0
                ? entry.OutputTypePins.Select(pin => pin.Name).ToList()
                : null;

            return new MethodEntryNodeDocument(entry.Id, null, null, entry.OutputDataPins.Count, genericArguments);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (MethodEntryNodeDocument)document;
            var entry = (MethodEntryNode)((MethodGraph)graph).EntryNode;

            for (int i = 0; i < doc.ArgumentCount; i++)
            {
                entry.AddArgument();
            }

            if (doc.GenericArguments is not null)
            {
                for (int i = 0; i < doc.GenericArguments.Count; i++)
                {
                    entry.AddGenericArgument();
                }

                for (int i = 0; i < doc.GenericArguments.Count; i++)
                {
                    entry.OutputTypePins[i].Name = doc.GenericArguments[i];
                }
            }

            return entry;
        }
    }

    private sealed class ConstructorEntryNodeConverter : INodeDocumentConverter
    {
        public string Kind => "constructorEntry";
        public Type NodeType => typeof(ConstructorEntryNode);
        public Type DocumentType => typeof(ConstructorEntryNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var entry = (ConstructorEntryNode)node;
            return new ConstructorEntryNodeDocument(entry.Id, null, null, entry.OutputDataPins.Count);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (ConstructorEntryNodeDocument)document;
            var entry = (ConstructorEntryNode)((ConstructorGraph)graph).EntryNode;

            if (doc.ArgumentCount != 0)
            {
                // ConstructorEntryNode has no argument mechanism yet (a pre-P1 "TODO: Add output data
                // and type pins for constructor graph"; T103a tracks the model gap). No document this
                // build produces ever sets a nonzero count; a hand-edited one claiming otherwise cannot
                // be honored.
                throw new DocumentFormatException(
                    $"Constructor argument count {doc.ArgumentCount} is not supported: constructor arguments are not implemented yet.");
            }

            return entry;
        }
    }

    private sealed class ReturnNodeConverter : INodeDocumentConverter
    {
        public string Kind => "return";
        public Type NodeType => typeof(ReturnNode);
        public Type DocumentType => typeof(ReturnNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var returnNode = (ReturnNode)node;
            return new ReturnNodeDocument(returnNode.Id, null, null, returnNode.InputDataPins.Count);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (ReturnNodeDocument)document;
            var methodGraph = (MethodGraph)graph;

            ReturnNode? mainReturn = context.ClaimMainReturnNode(methodGraph);
            if (mainReturn is not null)
            {
                for (int i = 0; i < doc.ReturnCount; i++)
                {
                    mainReturn.AddReturnType();
                }

                return mainReturn;
            }

            // A secondary return node: its pins replicate the main return node's automatically
            // (ReturnNode's constructor), so doc.ReturnCount (which always matches the main node's
            // count for a canonical document) needs no action here.
            return new ReturnNode(methodGraph);
        }
    }

    private sealed class ClassReturnNodeConverter : INodeDocumentConverter
    {
        public string Kind => "classReturn";
        public Type NodeType => typeof(ClassReturnNode);
        public Type DocumentType => typeof(ClassReturnNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var classReturn = (ClassReturnNode)node;
            return new ClassReturnNodeDocument(classReturn.Id, null, null, classReturn.InterfacePins.Count());
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (ClassReturnNodeDocument)document;
            var classReturn = ((ClassGraph)graph).ReturnNode;

            for (int i = 0; i < doc.InterfaceCount; i++)
            {
                classReturn.AddInterfacePin();
            }

            return classReturn;
        }
    }

    private sealed class TypeReturnNodeConverter : INodeDocumentConverter
    {
        public string Kind => "typeReturn";
        public Type NodeType => typeof(TypeReturnNode);
        public Type DocumentType => typeof(TypeReturnNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            return new TypeReturnNodeDocument(node.Id, null, null);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            return ((TypeGraph)graph).ReturnNode;
        }
    }
}
