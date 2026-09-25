#nullable enable
using System;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Raised by <see cref="NodeInputDataPin.IncomingPinChanged"/> after
    /// <see cref="NodeInputDataPin.IncomingPin"/> is set.
    /// </summary>
    /// <param name="pin">Pin whose incoming connection changed.</param>
    /// <param name="oldPin">Previously connected output data pin, or <see langword="null"/>.</param>
    /// <param name="newPin">Newly connected output data pin, or <see langword="null"/>.</param>
    public delegate void InputDataPinIncomingPinChangedDelegate(
        NodeInputDataPin pin, NodeOutputDataPin? oldPin, NodeOutputDataPin? newPin);

    /// <summary>
    /// Input data pin which can be connected to up to one output data pin to receive a value.
    /// </summary>
    [DataContract]
    public partial class NodeInputDataPin : NodeDataPin
    {
        /// <summary>
        /// Called when the node's incoming pin changed.
        /// </summary>
        public event InputDataPinIncomingPinChangedDelegate? IncomingPinChanged;

        /// <summary>
        /// Incoming data pin for this pin. Null when not connected.
        /// Can trigger IncomingPinChanged when set.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial NodeOutputDataPin? IncomingPin { get; set; }

        partial void OnIncomingPinChanged(NodeOutputDataPin? oldValue, NodeOutputDataPin? newValue) =>
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
        public partial object? UnconnectedValue { get; set; }

        partial void OnUnconnectedValueChanging(object? oldValue, object? newValue)
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

        /// <summary>
        /// The literal C# used as the argument's default value when this pin is unconnected and
        /// <see cref="UsesExplicitDefaultValue"/> is <see langword="false"/> and omitting the
        /// argument (relying on the parameter's own C# default) is not being used instead. Only
        /// meaningful for a <see cref="CallMethodNode"/> argument pin.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial object? ExplicitDefaultValue { get; set; }

        /// <summary>
        /// Whether this pin, when unconnected, omits the argument so the callee's own C# default
        /// value is used instead of emitting <see cref="ExplicitDefaultValue"/> or
        /// <see cref="UsesUnconnectedValue"/>'s value. Only meaningful for a
        /// <see cref="CallMethodNode"/> argument pin.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial bool UsesExplicitDefaultValue { get; set; }

        /// <summary>
        /// Creates an unconnected input data pin with no unconnected/default value set.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="pinType">Observable value carrying the pin's type.</param>
        public NodeInputDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name, pinType)
        {
        }
    }
}
