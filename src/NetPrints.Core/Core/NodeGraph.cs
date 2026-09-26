#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
        /// Id → node index backing <see cref="FindNode"/>. <see langword="null"/> until first needed:
        /// a graph deserialized by <see cref="System.Runtime.Serialization.DataContractSerializer"/>
        /// never runs this type's field initializers (document-format.md §3.1), so the index is built
        /// lazily instead, from whatever <see cref="Nodes"/> holds at that point, and kept current
        /// afterwards by <see cref="OnNodesChanged"/> (structural changes) and <see cref="ReindexNode"/>
        /// (a node's <see cref="Node.Id"/> changing after it was added).
        /// </summary>
        [IgnoreDataMember]
        private Dictionary<string, Node>? nodeIndex;

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
        /// Returns a new node id (data-model.md §2), drawn from <see cref="IdGeneration.Current"/>.
        /// Not retried or searched for: the generator guarantees uniqueness within its session. A
        /// caller that must tolerate a document with a pre-existing, colliding id (a merge or a
        /// hand-edited file) allocates against an explicit set of used ids instead
        /// (<see cref="StableIds.AllocateUnique"/>).
        /// </summary>
        /// <returns>A new node id.</returns>
        public string AllocateNodeId() => IdGeneration.Current.NewId('n');

        /// <summary>
        /// Returns the node of this graph whose <see cref="Node.Id"/> equals <paramref name="id"/>, in
        /// O(1) via <see cref="nodeIndex"/>.
        /// </summary>
        /// <param name="id">Node id to look up.</param>
        /// <returns>The matching node, or <see langword="null"/> if none has that id.</returns>
        public Node? FindNode(string id) => Index.TryGetValue(id, out Node? node) ? node : null;

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
                ReindexNode(Nodes[i], previousId: null);
            }
        }

        /// <summary>
        /// Updates <see cref="nodeIndex"/> after <paramref name="node"/>'s <see cref="Node.Id"/>
        /// changes: a mapper overwriting the constructor-assigned id with a document's id, legacy
        /// import assigning one for the first time, or load-time duplicate-id repair reassigning a
        /// fresh one (document-format.md §2.6). A no-op while the index has not been built yet
        /// (<see cref="FindNode"/> not yet called): it is built lazily from <see cref="Nodes"/>' then-
        /// current contents on first use, which already reflects the final id.
        /// </summary>
        /// <param name="node">Node whose id changed. Must belong to this graph.</param>
        /// <param name="previousId">The node's id before the change, or <see langword="null"/> if it
        /// had none yet.</param>
        internal void ReindexNode(Node node, string? previousId)
        {
            if (nodeIndex is null)
            {
                return;
            }

            if (previousId is not null && nodeIndex.TryGetValue(previousId, out Node? existing) && ReferenceEquals(existing, node))
            {
                nodeIndex.Remove(previousId);
            }

            if (node.Id is not null)
            {
                nodeIndex[node.Id] = node;
            }
        }

        private Dictionary<string, Node> Index
        {
            get
            {
                if (nodeIndex is null)
                {
                    var index = new Dictionary<string, Node>(StringComparer.Ordinal);
                    foreach (Node node in Nodes)
                    {
                        if (node.Id is not null)
                        {
                            index[node.Id] = node;
                        }
                    }

                    nodeIndex = index;
                    Nodes.CollectionChanged += OnNodesChanged;
                }

                return nodeIndex;
            }
        }

        private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Index, not the nodeIndex field directly: this handler only runs after Index's getter has
            // already built the dictionary and subscribed it, but the field's own type is nullable.
            Dictionary<string, Node> index = Index;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                    foreach (Node node in e.NewItems)
                    {
                        if (node.Id is not null)
                        {
                            index[node.Id] = node;
                        }
                    }

                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                    foreach (Node node in e.OldItems)
                    {
                        if (node.Id is not null && index.TryGetValue(node.Id, out Node? existing) && ReferenceEquals(existing, node))
                        {
                            index.Remove(node.Id);
                        }
                    }

                    break;

                default:
                    // Move/Replace/Reset (AddRange, RemoveRange, ReplaceRange): rebuild from scratch.
                    index.Clear();
                    foreach (Node node in Nodes)
                    {
                        if (node.Id is not null)
                        {
                            index[node.Id] = node;
                        }
                    }

                    break;
            }
        }
    }
}
