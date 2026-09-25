#nullable enable
using System;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing the initial execution node of a constructor.
    /// </summary>
    [DataContract]
    public class ConstructorEntryNode : ExecutionEntryNode
    {
        /// <summary>
        /// Same as the base <see cref="Node.Graph"/>, but typed as <see cref="ConstructorGraph"/>:
        /// the constructor only accepts one, so <see cref="Node.Graph"/> is always one for a
        /// <see cref="ConstructorEntryNode"/>.
        /// </summary>
        public ConstructorGraph ConstructorGraph
        {
            get => (ConstructorGraph)Graph;
        }

        /// <summary>
        /// Adds this node to <paramref name="constructor"/> and gives it its single output execution
        /// pin.
        /// </summary>
        /// <param name="constructor">Constructor graph the node belongs to.</param>
        public ConstructorEntryNode(ConstructorGraph constructor)
            : base(constructor)
        {
            AddOutputExecPin("Exec");

            // TODO: Add output data and type pins for constructor graph
        }

        /// <summary>
        /// Returns the declaring class's name (or "?" if the graph has no class yet) followed by
        /// " Constructor Entry".
        /// </summary>
        /// <returns>The declaring class's name, or "?", followed by " Constructor Entry".</returns>
        public override string ToString()
        {
            return $"{ConstructorGraph.Class?.Name ?? "?"} Constructor Entry";
        }
    }
}
