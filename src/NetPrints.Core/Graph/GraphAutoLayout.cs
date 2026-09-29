#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Places nodes that a loaded document had no <c>layout</c> entry for (document-format.md §2.6),
    /// so a hand-edited or partially authored graph document still opens with every node somewhere
    /// reasonable instead of stacked at the origin. Deterministic: the same graph and the same set of
    /// unpositioned nodes always produce the same positions.
    /// </summary>
    public static class GraphAutoLayout
    {
        /// <summary>Horizontal distance from a placed neighbour, and the fallback column's X offset
        /// past the rightmost already-placed node.</summary>
        public const int ColumnSpacing = 300;

        /// <summary>Vertical step between successive fallback-column nodes, and between collision
        /// retries.</summary>
        public const int RowSpacing = 120;

        /// <summary>Horizontal extent of the collision check around a candidate position.</summary>
        public const int CollisionWidth = 200;

        /// <summary>Vertical extent of the collision check around a candidate position.</summary>
        public const int CollisionHeight = 100;

        /// <summary>
        /// Assigns <see cref="Node.PositionX"/>/<see cref="Node.PositionY"/> to every node in
        /// <paramref name="unpositioned"/> (data-model.md §2), processed in <paramref name="graph"/>'s
        /// <see cref="NodeGraph.Nodes"/> order. For each node: a placed neighbour reachable through an
        /// input pin (exec, then data, then type, each in collection order) places it one
        /// <see cref="ColumnSpacing"/> to the neighbour's right at the same height; failing that, a
        /// placed neighbour reachable through an output pin (same order) places it one
        /// <see cref="ColumnSpacing"/> to the neighbour's left; failing that, it falls into a fallback
        /// column one <see cref="ColumnSpacing"/> past the rightmost node that already had a position
        /// (or column 0 if none did), stepping down by <see cref="RowSpacing"/> for each successive
        /// fallback node. Either way, if the candidate lands within <see cref="CollisionWidth"/> ×
        /// <see cref="CollisionHeight"/> of an already-placed node, it moves down by
        /// <see cref="RowSpacing"/> and is re-checked. A node this call places counts as placed for
        /// the nodes processed after it.
        /// </summary>
        /// <param name="graph">Graph the nodes belong to.</param>
        /// <param name="unpositioned">Nodes of <paramref name="graph"/> to place.</param>
        public static void PlaceUnpositioned(NodeGraph graph, IReadOnlyCollection<Node> unpositioned)
        {
            var toPlace = new HashSet<Node>(unpositioned);
            var placed = new HashSet<Node>(graph.Nodes.Where(n => !toPlace.Contains(n)));

            double fallbackX;
            double fallbackMinY;

            if (placed.Count == 0)
            {
                fallbackX = 0;
                fallbackMinY = 0;
            }
            else
            {
                fallbackX = placed.Max(n => n.PositionX) + ColumnSpacing;
                fallbackMinY = placed.Min(n => n.PositionY);
            }

            int fallbackIndex = 0;

            foreach (Node node in graph.Nodes)
            {
                if (!toPlace.Contains(node))
                {
                    continue;
                }

                double x;
                double y;

                Node? upstream = FindNeighbour(node, incoming: true, placed);
                if (upstream is not null)
                {
                    x = upstream.PositionX + ColumnSpacing;
                    y = upstream.PositionY;
                }
                else
                {
                    Node? downstream = FindNeighbour(node, incoming: false, placed);
                    if (downstream is not null)
                    {
                        x = downstream.PositionX - ColumnSpacing;
                        y = downstream.PositionY;
                    }
                    else
                    {
                        x = fallbackX;
                        y = fallbackMinY + (fallbackIndex * RowSpacing);
                        fallbackIndex++;
                    }
                }

                (x, y) = ResolveCollisions(x, y, placed);

                node.PositionX = x;
                node.PositionY = y;

                placed.Add(node);
            }
        }

        /// <summary>
        /// Returns the first already-placed node reachable from <paramref name="node"/>'s input pins
        /// (<paramref name="incoming"/> <see langword="true"/>) or output pins
        /// (<paramref name="incoming"/> <see langword="false"/>), checked in exec, then data, then
        /// type, collection order; for a pin with more than one connection, its own connections are
        /// checked in their collection order.
        /// </summary>
        private static Node? FindNeighbour(Node node, bool incoming, HashSet<Node> placed)
        {
            if (incoming)
            {
                foreach (NodeInputExecPin pin in node.InputExecPins)
                {
                    foreach (NodeOutputExecPin source in pin.IncomingPins)
                    {
                        if (placed.Contains(source.Node))
                        {
                            return source.Node;
                        }
                    }
                }

                foreach (NodeInputDataPin pin in node.InputDataPins)
                {
                    if (pin.IncomingPin is { } source && placed.Contains(source.Node))
                    {
                        return source.Node;
                    }
                }

                foreach (NodeInputTypePin pin in node.InputTypePins)
                {
                    if (pin.IncomingPin is { } source && placed.Contains(source.Node))
                    {
                        return source.Node;
                    }
                }
            }
            else
            {
                foreach (NodeOutputExecPin pin in node.OutputExecPins)
                {
                    if (pin.OutgoingPin is { } target && placed.Contains(target.Node))
                    {
                        return target.Node;
                    }
                }

                foreach (NodeOutputDataPin pin in node.OutputDataPins)
                {
                    foreach (NodeInputDataPin target in pin.OutgoingPins)
                    {
                        if (placed.Contains(target.Node))
                        {
                            return target.Node;
                        }
                    }
                }

                foreach (NodeOutputTypePin pin in node.OutputTypePins)
                {
                    foreach (NodeInputTypePin target in pin.OutgoingPins)
                    {
                        if (placed.Contains(target.Node))
                        {
                            return target.Node;
                        }
                    }
                }
            }

            return null;
        }

        private static (double X, double Y) ResolveCollisions(double x, double y, HashSet<Node> placed)
        {
            bool collided = true;

            while (collided)
            {
                collided = false;

                foreach (Node other in placed)
                {
                    if (Math.Abs(other.PositionX - x) < CollisionWidth && Math.Abs(other.PositionY - y) < CollisionHeight)
                    {
                        y += RowSpacing;
                        collided = true;
                        break;
                    }
                }
            }

            return (x, y);
        }
    }
}
