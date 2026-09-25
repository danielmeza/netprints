#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
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

        public override ObservableValue<BaseType>? InferredType
        {
            get => IncomingPin?.InferredType;
        }

        public NodeInputTypePin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
