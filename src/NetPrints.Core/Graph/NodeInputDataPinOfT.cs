#nullable enable
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// An input data pin whose type is the compile-time type <typeparamref name="T"/>. Its unconnected value starts as
    /// <c>default(<typeparamref name="T"/>)</c> where the type has an editable unconnected value.
    /// </summary>
    /// <typeparam name="T">The type of the values the pin carries; no constraint.</typeparam>
    public class NodeInputDataPin<T> : NodeInputDataPin
    {
        /// <summary>Creates a pin typed from <typeparamref name="T"/>.</summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        public NodeInputDataPin(Node node, string name)
            : this(node, name, new ObservableValue<BaseType>(TypeSpecifier.FromType<T>()))
        {
        }

        /// <summary>Creates a pin whose type is carried by an observable value, for pins declared with <c>[PinType]</c>.</summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        /// <param name="pinType">Observable value carrying the pin's type.</param>
        public NodeInputDataPin(Node node, string name, ObservableValue<BaseType> pinType)
            : base(node, name, pinType)
        {
            object? initial = default(T);
            if (initial is not null && !typeof(T).IsEnum && UsesUnconnectedValue)
            {
                UnconnectedValue = initial;
            }
        }
    }
}
