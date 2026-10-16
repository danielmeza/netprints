#nullable enable
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>An output data pin whose type is the compile-time type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type of the values the pin carries; no constraint.</typeparam>
    public class NodeOutputDataPin<T> : NodeOutputDataPin
    {
        /// <summary>Creates a pin typed from <typeparamref name="T"/>.</summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        public NodeOutputDataPin(Node node, string name)
            : this(node, name, new ObservableValue<BaseType>(TypeSpecifier.FromType<T>()))
        {
        }

        /// <summary>Creates a pin whose type is carried by an observable value, for pins declared with <c>[PinType]</c>.</summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="pinType">Observable value carrying the pin's type.</param>
        public NodeOutputDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name, pinType)
        {
        }
    }
}
