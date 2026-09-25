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

        /// <summary>
        /// The type this pin outputs. Always set by the constructor: narrower than the base
        /// <see cref="NodeTypePin.InferredType"/> (an output type pin's inferred type is never
        /// absent, unlike an unconnected input type pin's).
        /// </summary>
        public override ObservableValue<BaseType> InferredType
        {
            get => outputType;
        }

        [DataMember]
        private ObservableValue<BaseType> outputType;

        /// <summary>
        /// Creates an output type pin carrying <paramref name="outputType"/>.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="outputType">Observable value carrying the pin's output type.</param>
        public NodeOutputTypePin(Node node, string name, ObservableValue<BaseType> outputType)
            : base(node, name)
        {
            this.outputType = outputType;
        }

        /// <summary>
        /// Returns the output type's short name, or "None" if it has not been set.
        /// </summary>
        /// <returns>The output type's short name, or "None".</returns>
        public override string ToString()
        {
            return outputType.Value?.ShortName ?? "None";
        }
    }
}
