#nullable enable
using System;
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
        /// Renames this event graph. The name is a label, so the generated C# does not change.
        /// </summary>
        /// <param name="newName">The new name; it must not be used by another event graph of the class.</param>
        /// <returns>A handle that restores the old name.</returns>
        /// <exception cref="ArgumentException"><paramref name="newName"/> is blank, or another event graph of the class already has it.</exception>
        public RenameResult Rename(string newName)
        {
            ArgumentNullException.ThrowIfNull(newName);
            newName = newName.Trim();

            if (newName.Length == 0)
            {
                throw new ArgumentException("An event graph name cannot be blank", nameof(newName));
            }

            if (Class is not null && Class.EventGraphs.Any(other => !ReferenceEquals(other, this) && other.Name == newName))
            {
                throw new ArgumentException($"An event graph named '{newName}' already exists", nameof(newName));
            }

            string oldName = Name;
            Name = newName;
            return new RenameResult(() => Name = oldName);
        }

        /// <summary>
        /// Returns <see cref="Name"/>.
        /// </summary>
        /// <returns><see cref="Name"/>.</returns>
        public override string ToString() => Name;
    }
}
