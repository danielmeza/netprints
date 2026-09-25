#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Graph
{
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

        public NodeOutputExecPin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
