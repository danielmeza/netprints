#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping.BuiltIn;

/// <summary>
/// Converter for an event graph's entry node (document-format.md §1.5, sub-phase G). Unlike
/// <see cref="EntryReturnConverters"/>'s fixed nodes, an <see cref="EventGraph"/> starts empty: each
/// <see cref="EventEntryNode"/> is created fresh with <see langword="new"/>, like
/// <see cref="MemberConverters"/>'s nodes.
/// </summary>
internal static class EventConverters
{
    /// <summary>Every converter this file contributes.</summary>
    public static IReadOnlyList<INodeDocumentConverter> All { get; } = [new EventEntryNodeConverter()];

    private sealed class EventEntryNodeConverter : INodeDocumentConverter
    {
        public string Kind => BuiltInNodeKinds.EventEntry;
        public Type NodeType => typeof(EventEntryNode);
        public Type DocumentType => typeof(EventEntryNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var entry = (EventEntryNode)node;
            MethodRef? overrides = entry.OverriddenMethod is { } overridden ? context.ToRef(overridden) : null;

            return new EventEntryNodeDocument(entry.Id, null, null, entry.EventName, entry.Visibility,
                entry.Modifiers, overrides, entry.OutputDataPins.Count);
        }

        public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context)
        {
            var doc = (EventEntryNodeDocument)document;
            var eventGraph = (EventGraph)graph;

            EventEntryNode entry;
            if (doc.Overrides is { } overrides)
            {
                // The override constructor already adds one output data pin per base parameter
                // (data-model.md §4); doc.ArgumentCount always matches that count for a canonical
                // document and needs no separate action.
                entry = new EventEntryNode(eventGraph, context.FromRef(overrides));
            }
            else
            {
                entry = new EventEntryNode(eventGraph, doc.EventName);

                for (int i = 0; i < doc.ArgumentCount; i++)
                {
                    entry.AddArgument();
                }
            }

            entry.Visibility = doc.Visibility;
            entry.Modifiers = doc.Modifiers;

            return entry;
        }
    }
}
