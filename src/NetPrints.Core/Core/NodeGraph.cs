#nullable enable
using System;
using System.Runtime.Serialization;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Abstract base class for every kind of node graph: <see cref="MethodGraph"/>,
    /// <see cref="ConstructorGraph"/>, <see cref="ClassGraph"/> and <see cref="TypeGraph"/>. Holds the
    /// graph's node collection and its owning class and project.
    /// </summary>
    [DataContract]
    [KnownType(typeof(MethodGraph))]
    [KnownType(typeof(ConstructorGraph))]
    [KnownType(typeof(ClassGraph))]
    [KnownType(typeof(TypeGraph))]
    public abstract class NodeGraph
    {
        /// <summary>
        /// Maximum number of ids <see cref="AllocateNodeId"/> tries before giving up.
        /// </summary>
        private const int MaxAllocateAttempts = 100;

        /// <summary>
        /// Collection of nodes in this graph.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<Node> Nodes
        {
            get;
            private set;
        } = new ObservableRangeCollection<Node>();

        /// <summary>
        /// Class this graph is contained in.
        /// </summary>
        [DataMember]
        public ClassGraph? Class
        {
            get;
            set;
        }

        /// <summary>
        /// Project the graph is part of.
        /// </summary>
        public Project? Project
        {
            get;
            set;
        }

        /// <summary>
        /// Opaque state for nodes this graph's document contained but that no converter could
        /// recreate (an unknown or untrusted extension node kind). Owned and interpreted by
        /// <c>NetPrints.Serialization</c>; not serialized by <see cref="System.Runtime.Serialization.DataContractSerializer"/>
        /// and not otherwise inspected by <c>NetPrints.Core</c>.
        /// </summary>
        [IgnoreDataMember]
        public object? PreservedDocumentState { get; set; }

        /// <summary>
        /// Returns a new node id (data-model.md §2), drawn from <see cref="IdGeneration.Current"/> and
        /// retried until it is not already used by a node of this graph.
        /// </summary>
        /// <returns>A node id unique in this graph.</returns>
        /// <exception cref="InvalidOperationException">No unused id was found in
        /// <see cref="MaxAllocateAttempts"/> tries.</exception>
        public string AllocateNodeId()
        {
            for (int attempt = 0; attempt < MaxAllocateAttempts; attempt++)
            {
                string candidate = IdGeneration.Current.NewId('n');
                if (FindNode(candidate) is null)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException($"Could not allocate a unique node id in {MaxAllocateAttempts} attempts.");
        }

        /// <summary>
        /// Returns the node of this graph whose <see cref="Node.Id"/> equals <paramref name="id"/>.
        /// </summary>
        /// <param name="id">Node id to look up.</param>
        /// <returns>The matching node, or <see langword="null"/> if none has that id.</returns>
        public Node? FindNode(string id)
        {
            foreach (Node node in Nodes)
            {
                if (node.Id == id)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Assigns every node of this graph an id derived from its position in <see cref="Nodes"/>
        /// (<c>"n0"</c>, <c>"n1"</c>, …), for a legacy document that had no ids of its own. Only valid
        /// immediately after <see cref="System.Runtime.Serialization.DataContractSerializer"/>
        /// deserialization, before anything reads a node's <see cref="Node.Id"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">A node of this graph already has an id.</exception>
        public void AssignLegacyNodeIds()
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id is not null)
                {
                    throw new InvalidOperationException($"Node at index {i} already has an id ('{Nodes[i].Id}').");
                }

                Nodes[i].Id = $"n{i}";
            }
        }
    }
}
