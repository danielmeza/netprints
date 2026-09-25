#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for type pins.
    /// </summary>
    [DataContract]
    public abstract class NodeTypePin : NodePin
    {
        /// <summary>
        /// The type this pin currently carries, or <see langword="null"/> if none has been inferred
        /// yet (an unconnected <see cref="NodeInputTypePin"/>). Wrapped in an
        /// <see cref="ObservableValue{T}"/> so dependents can react to it changing in place.
        /// </summary>
        public abstract ObservableValue<BaseType>? InferredType
        {
            get;
        }

        /// <summary>
        /// Forwards to <see cref="NodePin"/>'s constructor; type pins have no state of their own
        /// beyond <see cref="InferredType"/>, which the derived class initializes.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        protected NodeTypePin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
