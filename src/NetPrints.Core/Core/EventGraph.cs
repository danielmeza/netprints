#nullable enable
using System.Collections.Generic;
using System.Linq;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// A class's event graph (US4, data-model.md §4): a <see cref="NodeGraph"/> holding one or more
    /// <see cref="EventEntryNode"/>s, each translated into its own method. Unlike
    /// <see cref="MethodGraph"/>/<see cref="ConstructorGraph"/> it has no single fixed entry node: it
    /// starts empty, and several independent entries (custom events, base method overrides) can share
    /// one visual graph.
    /// </summary>
    public sealed class EventGraph : NodeGraph
    {
        /// <summary>Prefix of a new event graph's generated unique name (EventGraph, EventGraph1, ...).</summary>
        public const string DefaultNamePrefix = "EventGraph";

        /// <summary>
        /// This event graph's member id (data-model.md §2), used as its graph key. Assigned once, in
        /// the constructor, from <see cref="IdGeneration.Current"/>; the mapper overwrites it from the
        /// document.
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// Name of this event graph. A label for the graph itself, not a generated member: each
        /// <see cref="EventEntryNode.EventName"/> names its own generated method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// This event graph's entries, in node order (<see cref="NodeGraph.Nodes"/>). Each is
        /// translated into its own method, in this order.
        /// </summary>
        public IEnumerable<EventEntryNode> Entries => Nodes.OfType<EventEntryNode>();

        /// <summary>
        /// Creates an empty event graph given its name.
        /// </summary>
        /// <param name="name">Name for the event graph.</param>
        public EventGraph(string name)
        {
            Id = IdGeneration.Current.NewId('m');
            Name = name;
        }

        /// <summary>
        /// Returns <see cref="Name"/>.
        /// </summary>
        /// <returns><see cref="Name"/>.</returns>
        public override string ToString() => Name;
    }
}
