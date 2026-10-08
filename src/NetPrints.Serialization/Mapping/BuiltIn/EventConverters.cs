#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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

    private static readonly TypeSpecifier ObjectType = TypeSpecifier.FromType<object>();

    private const string ArgumentPinKeyPrefix = "out.data.Input";

    /// <summary>
    /// The names of <paramref name="doc"/>'s custom arguments: the written name when it is a usable
    /// identifier, else the name stored in the argument's pin state, else <c>InputN</c>. Each repaired
    /// argument adds a message to <paramref name="repairs"/>.
    /// </summary>
    internal static List<string> ResolveNames(EventEntryNodeDocument doc, List<string> repairs)
    {
        List<string> names = [];
        HashSet<string> used = [];
        IReadOnlyList<EventArgumentDocument> arguments = doc.Arguments ?? [];

        for (int i = 0; i < arguments.Count; i++)
        {
            string defaultName = $"Input{i}";
            string pinName = doc.Pins?.FirstOrDefault(pin => pin.Pin == ArgumentPinKeyPrefix + i)?.Name ?? defaultName;
            string? written = arguments[i].Name;

            string name;
            if (written is null)
            {
                name = Usable(pinName) ? pinName : Fallback();
            }
            else if (Usable(written))
            {
                name = written;
            }
            else
            {
                name = Usable(pinName) ? pinName : Fallback();
                repairs.Add($"Event '{doc.EventName}' argument {i} has the name '{written}', which is not a valid or unique identifier; it is now '{name}'.");
            }

            used.Add(name);
            names.Add(name);

            bool Usable(string candidate) => EventEntryNode.IsValidArgumentName(candidate) && !used.Contains(candidate);

            string Fallback()
            {
                string candidate = defaultName;
                while (!Usable(candidate))
                {
                    candidate += "_";
                }

                return candidate;
            }
        }

        return names;
    }

    private sealed class EventEntryNodeConverter : INodeDocumentConverter
    {
        public string Kind => BuiltInNodeKinds.EventEntry;
        public Type NodeType => typeof(EventEntryNode);
        public Type DocumentType => typeof(EventEntryNodeDocument);

        public NodeDocument ToDocument(Node node, NodeMappingContext context)
        {
            var entry = (EventEntryNode)node;
            MethodRef? overrides = entry.OverriddenMethod is { } overridden ? context.ToRef(overridden) : null;

            IReadOnlyList<EventArgument> typed = entry.DeclaredArguments;
            IReadOnlyList<EventArgumentDocument>? arguments = overrides is null && typed.Any(argument => !argument.Type.Equals(ObjectType))
                ? typed.Select(argument => new EventArgumentDocument(null, context.ToRef(argument.Type))).ToList()
                : null;

            return new EventEntryNodeDocument(entry.Id, null, null, entry.EventName, entry.Visibility,
                entry.Modifiers, overrides, entry.OutputDataPins.Count, arguments);
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

                if (doc.Arguments is { Count: > 0 } arguments)
                {
                    List<string> names = ResolveNames(doc, []);
                    List<EventArgument> typed = arguments
                        .Select((argument, i) => new EventArgument(names[i], context.FromTypeRef(argument.Type, $"Event '{doc.EventName}' argument '{names[i]}' type")))
                        .ToList();

                    entry.SetArguments(typed);
                }
            }

            entry.Visibility = doc.Visibility;
            entry.Modifiers = doc.Modifiers;

            return entry;
        }
    }
}
