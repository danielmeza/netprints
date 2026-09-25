#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Raised by <see cref="NodeInputTypePin.IncomingPinChanged"/> after
    /// <see cref="NodeInputTypePin.IncomingPin"/> is set.
    /// </summary>
    /// <param name="pin">Pin whose incoming connection changed.</param>
    /// <param name="oldPin">Previously connected output type pin, or <see langword="null"/>.</param>
    /// <param name="newPin">Newly connected output type pin, or <see langword="null"/>.</param>
    public delegate void InputTypePinIncomingPinChangedDelegate(
        NodeInputTypePin pin, NodeOutputTypePin? oldPin, NodeOutputTypePin? newPin);

    /// <summary>
    /// Pin which can receive types.
    /// </summary>
    [DataContract]
    public partial class NodeInputTypePin : NodeTypePin
    {
        /// <summary>
        /// Called when the node's incoming pin changed.
        /// </summary>
        public event InputTypePinIncomingPinChangedDelegate? IncomingPinChanged;

        /// <summary>
        /// Incoming type pin for this pin. Null when not connected.
        /// Can trigger IncomingPinChanged when set.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InferredType))]
        [DataMember]
        public partial NodeOutputTypePin? IncomingPin { get; set; }

        partial void OnIncomingPinChanged(NodeOutputTypePin? oldValue, NodeOutputTypePin? newValue) =>
            IncomingPinChanged?.Invoke(this, oldValue, newValue);

        /// <summary>
        /// The connected pin's inferred type, or <see langword="null"/> if unconnected.
        /// </summary>
        public override ObservableValue<BaseType>? InferredType
        {
            get => IncomingPin?.InferredType;
        }

        /// <summary>
        /// Creates an input type pin with no incoming connection.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        public NodeInputTypePin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
