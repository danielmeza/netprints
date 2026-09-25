#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Graph
{
    /// <summary>
    /// Raised by <see cref="NodeOutputExecPin.OutgoingPinChanged"/> after
    /// <see cref="NodeOutputExecPin.OutgoingPin"/> is set.
    /// </summary>
    /// <param name="pin">Pin whose outgoing connection changed.</param>
    /// <param name="oldPin">Previously connected input execution pin, or <see langword="null"/>.</param>
    /// <param name="newPin">Newly connected input execution pin, or <see langword="null"/>.</param>
    public delegate void OutputExecPinOutgoingPinChangedDelegate(
        NodeOutputExecPin pin, NodeInputExecPin? oldPin, NodeInputExecPin? newPin);

    /// <summary>
    /// Pin which can be connected to an input execution pin to pass along execution.
    /// </summary>
    [DataContract]
    public partial class NodeOutputExecPin : NodeExecPin
    {
        /// <summary>
        /// Called when the connected outgoing pin changed.
        /// </summary>
        public event OutputExecPinOutgoingPinChangedDelegate? OutgoingPinChanged;

        /// <summary>
        /// Connected input execution pin. Null if not connected.
        /// Can trigger OutgoingPinChanged when set.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial NodeInputExecPin? OutgoingPin { get; set; }

        partial void OnOutgoingPinChanged(NodeInputExecPin? oldValue, NodeInputExecPin? newValue) =>
            OutgoingPinChanged?.Invoke(this, oldValue, newValue);

        /// <summary>
        /// Creates an output execution pin with no outgoing connection.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        public NodeOutputExecPin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
