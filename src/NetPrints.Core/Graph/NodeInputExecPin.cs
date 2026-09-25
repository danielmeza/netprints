#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Pin that can be connected to output execution pins to receive execution.
    /// </summary>
    [DataContract]
    public class NodeInputExecPin : NodeExecPin
    {
        /// <summary>
        /// Output execution pins connected to this pin.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeOutputExecPin> IncomingPins { get; private set; } =
            new ObservableRangeCollection<NodeOutputExecPin>();

        /// <summary>
        /// Creates an input execution pin with no incoming connections.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        public NodeInputExecPin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
