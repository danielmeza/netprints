#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// A <see cref="TypeGraph"/>'s single fixed node, analogous to <see cref="ClassReturnNode"/>: it
    /// holds the graph's resolved type as a single input type pin.
    /// </summary>
    [DataContract]
    public class TypeReturnNode : Node
    {
        /// <summary>
        /// Input type pin for the graph's resolved type.
        /// </summary>
        public NodeInputTypePin TypePin => InputTypePins[0];

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its type input type pin.
        /// </summary>
        /// <param name="graph">Type graph the node belongs to.</param>
        public TypeReturnNode(TypeGraph graph)
            : base(graph)
        {
            AddInputTypePin("Type");
        }
    }
}
