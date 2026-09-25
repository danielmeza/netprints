#nullable enable
using System;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node that returns the default value of a type.
    /// </summary>
    [DataContract]
    public class DefaultNode : Node
    {
        /// <summary>
        /// Pin for the default value.
        /// </summary>
        public NodeOutputDataPin DefaultValuePin
        {
            get { return OutputDataPins[0]; }
        }

        /// <summary>
        /// Input type pin for the type to cast to.
        /// </summary>
        public NodeInputTypePin TypePin
        {
            get { return InputTypePins[0]; }
        }

        /// <summary>
        /// Type for the default value output. Inferred from input type pin.
        /// </summary>
        public BaseType Type
        {
            get => TypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its type and default-value pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public DefaultNode(NodeGraph graph)
            : base(graph)
        {
            AddInputTypePin("Type");
            AddOutputDataPin("Default", Type);
        }

        /// <summary>
        /// Propagates <see cref="Type"/> (the inferred type) to <see cref="DefaultValuePin"/>.
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);

            DefaultValuePin.PinType.Value = Type;
        }

        /// <summary>
        /// Returns "Default " followed by the type's short name.
        /// </summary>
        /// <returns>"Default " followed by the type's short name.</returns>
        public override string ToString()
        {
            return $"Default {Type.ShortName}";
        }
    }
}
