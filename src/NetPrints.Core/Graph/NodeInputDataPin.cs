using System;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    public delegate void InputDataPinIncomingPinChangedDelegate(
        NodeInputDataPin pin, NodeOutputDataPin oldPin, NodeOutputDataPin newPin);

    /// <summary>
    /// Input data pin which can be connected to up to one output data pin to receive a value.
    /// </summary>
    [DataContract]
    public partial class NodeInputDataPin : NodeDataPin
    {
        /// <summary>
        /// Called when the node's incoming pin changed.
        /// </summary>
        public event InputDataPinIncomingPinChangedDelegate IncomingPinChanged;

        /// <summary>
        /// Incoming data pin for this pin. Null when not connected.
        /// Can trigger IncomingPinChanged when set.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial NodeOutputDataPin IncomingPin { get; set; }

        partial void OnIncomingPinChanged(NodeOutputDataPin oldValue, NodeOutputDataPin newValue) =>
            IncomingPinChanged?.Invoke(this, oldValue, newValue);

        /// <summary>
        /// Whether this pin uses its unconnected value to output a value
        /// when no pin is connected to it.
        /// </summary>
        public bool UsesUnconnectedValue
        {
            get => PinType.Value is TypeSpecifier t && t.IsPrimitive;
        }

        /// <summary>
        /// Unconnected value of this pin when no pin is connected to it.
        /// Setting this for types that don't support unconnected values will throw
        /// an exception.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial object UnconnectedValue { get; set; }

        partial void OnUnconnectedValueChanging(object oldValue, object newValue)
        {
            // Check that:
            // this pin uses the unconnected value
            // the value is of the same type or string if enum

            if (newValue != null && (!UsesUnconnectedValue
                || (PinType.Value is TypeSpecifier t && (
                    (!t.IsEnum && TypeSpecifier.FromType(newValue.GetType()) != t)
                    || (t.IsEnum && newValue.GetType() != typeof(string))))))
            {
                throw new ArgumentException();
            }
        }

        [ObservableProperty]
        [DataMember]
        public partial object ExplicitDefaultValue { get; set; }

        [ObservableProperty]
        [DataMember]
        public partial bool UsesExplicitDefaultValue { get; set; }

        public NodeInputDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name, pinType)
        {
        }
    }
}
