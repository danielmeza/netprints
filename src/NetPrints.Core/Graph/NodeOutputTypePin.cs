#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Pin which outputs a type. Can be connected to input type pins.
    /// </summary>
    [DataContract]
    public class NodeOutputTypePin : NodeTypePin
    {
        /// <summary>
        /// Connected input data pins.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeInputTypePin> OutgoingPins { get; private set; }
            = new ObservableRangeCollection<NodeInputTypePin>();

        // Always set by the constructor: narrower than the base NodeTypePin.InferredType (an output
        // type pin's inferred type is never absent, unlike an unconnected input type pin's).
        public override ObservableValue<BaseType> InferredType
        {
            get => outputType;
        }

        [DataMember]
        private ObservableValue<BaseType> outputType;

        public NodeOutputTypePin(Node node, string name, ObservableValue<BaseType> outputType)
            : base(node, name)
        {
            this.outputType = outputType;
        }

        public override string ToString()
        {
            return outputType.Value?.ShortName ?? "None";
        }
    }
}
