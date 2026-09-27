#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Abstract base class for every kind of node graph: <see cref="MethodGraph"/>,
    /// <see cref="ConstructorGraph"/>, <see cref="ClassGraph"/> and <see cref="TypeGraph"/>. Holds the
    /// graph's node collection and its owning class and project.
    /// </summary>
    public abstract class NodeGraph
    {
        /// <summary>
        /// Id → node index backing <see cref="FindNode"/>, kept current by <see cref="OnNodesChanged"/>
        /// (structural changes) and <see cref="ReindexNode"/> (a node's <see cref="Node.Id"/> changing
        /// after it was added).
        /// </summary>
        private readonly Dictionary<string, Node> nodeIndex = new(StringComparer.Ordinal);

        /// <summary>
        /// Collection of nodes in this graph.
        /// </summary>
        public ObservableRangeCollection<Node> Nodes
        {
            get;
            private set;
        } = new ObservableRangeCollection<Node>();

        /// <summary>
        /// Starts tracking <see cref="Nodes"/> in the id index.
        /// </summary>
        protected NodeGraph()
        {
            Nodes.CollectionChanged += OnNodesChanged;
        }

        /// <summary>
        /// Class this graph is contained in.
        /// </summary>
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
        /// <c>NetPrints.Serialization</c>; not otherwise inspected by <c>NetPrints.Core</c>.
        /// </summary>
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
        public Node? FindNode(string id) => nodeIndex.TryGetValue(id, out Node? node) ? node : null;

        /// <summary>
        /// Updates <see cref="nodeIndex"/> after <paramref name="node"/>'s <see cref="Node.Id"/>
        /// changes: a mapper overwriting the constructor-assigned id with a document's id, or
        /// load-time duplicate-id repair reassigning a fresh one (document-format.md §2.6).
        /// </summary>
        /// <param name="node">Node whose id changed. Must belong to this graph.</param>
        /// <param name="previousId">The node's id before the change, or <see langword="null"/> if it
        /// had none yet.</param>
        internal void ReindexNode(Node node, string? previousId)
        {
            if (previousId is not null && nodeIndex.TryGetValue(previousId, out Node? existing) && ReferenceEquals(existing, node))
            {
                nodeIndex.Remove(previousId);
            }

            nodeIndex[node.Id] = node;
        }

        private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                    foreach (Node node in e.NewItems)
                    {
                        nodeIndex[node.Id] = node;
                    }

                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                    foreach (Node node in e.OldItems)
                    {
                        if (nodeIndex.TryGetValue(node.Id, out Node? existing) && ReferenceEquals(existing, node))
                        {
                            nodeIndex.Remove(node.Id);
                        }
                    }

                    break;

                default:
                    // Move/Replace/Reset (AddRange, RemoveRange, ReplaceRange): rebuild from scratch.
                    nodeIndex.Clear();
                    foreach (Node node in Nodes)
                    {
                        nodeIndex[node.Id] = node;
                    }

                    break;
            }
        }
    }
}
