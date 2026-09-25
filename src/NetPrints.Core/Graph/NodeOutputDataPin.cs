#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Pin which outputs a value. Can be connected to input data pins.
    /// </summary>
    [DataContract]
    public class NodeOutputDataPin : NodeDataPin
    {
        /// <summary>
        /// Connected input data pins.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeInputDataPin> OutgoingPins { get; private set; }
            = new ObservableRangeCollection<NodeInputDataPin>();

        /// <summary>
        /// Creates an output data pin with no connected input pins yet.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="pinType">Observable value carrying the pin's type.</param>
        public NodeOutputDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name, pinType)
        {
        }
    }
}
