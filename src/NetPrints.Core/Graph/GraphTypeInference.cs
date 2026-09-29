#nullable enable
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Re-runs each node's type inference (<see cref="Node.OnMethodDeserialized"/>) until every input
    /// type pin's inferred type stops changing, or a maximum number of iterations is reached. Used
    /// after building a graph's nodes and connections from something other than a live editor session
    /// (a loaded document or a legacy import): a node's inferred type can depend on another node's,
    /// which itself may still be settling, so a single top-to-bottom pass is not always enough
    /// (document-format.md §3.1).
    /// </summary>
    public static class GraphTypeInference
    {
        /// <summary>Maximum number of passes before giving up on the types settling.</summary>
        private const int MaxIterations = 20;

        /// <summary>
        /// Repeatedly calls <see cref="Node.OnMethodDeserialized"/> on every node of
        /// <paramref name="graph"/>, in <see cref="NodeGraph.Nodes"/> order, until no input type
        /// pin's <see cref="NodeTypePin.InferredType"/> changes in a full pass, or
        /// <see cref="MaxIterations"/> passes have run.
        /// </summary>
        /// <param name="graph">Graph whose nodes' types should settle.</param>
        public static void Relax(NodeGraph graph)
        {
            int iterations = 0;
            bool anyTypeChanged = true;
            var pinTypes = new Dictionary<NodeTypePin, BaseType?>();

            while (anyTypeChanged && iterations < MaxIterations)
            {
                anyTypeChanged = false;
                pinTypes.Clear();

                foreach (Node node in graph.Nodes)
                {
                    foreach (NodeInputTypePin pin in node.InputTypePins)
                    {
                        pinTypes.Add(pin, pin.InferredType?.Value);
                    }

                    node.OnMethodDeserialized();

                    if (node.InputTypePins.Any(p => pinTypes[p] != p.InferredType?.Value))
                    {
                        anyTypeChanged = true;
                    }
                }

                iterations++;
            }
        }
    }
}
