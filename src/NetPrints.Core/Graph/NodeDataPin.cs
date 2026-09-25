#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for data pins.
    /// </summary>
    [DataContract]
    public abstract class NodeDataPin : NodePin
    {
        /// <summary>
        /// Specifier for the type of this data pin.
        /// </summary>
        [DataMember]
        public ObservableValue<BaseType> PinType { get; private set; }

        /// <summary>
        /// Sets <see cref="PinType"/> in addition to <see cref="NodePin"/>'s base state.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="pinType">Observable value carrying the pin's type.</param>
        protected NodeDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name)
        {
            PinType = pinType;
        }

        /// <summary>
        /// Returns <see cref="NodePin.Name"/> and the pin's resolved type's short name, or "?" if the
        /// type has not been resolved yet.
        /// </summary>
        /// <returns>A string of the form "name: type".</returns>
        public override string ToString()
        {
            return $"{Name}: {PinType.Value?.ShortName ?? "?"}";
        }
    }
}
