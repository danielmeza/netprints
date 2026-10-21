#nullable enable
using System;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Pure type node producing the array type of its input element type (eg. <c>int</c> ->
    /// <c>int[]</c>), as a single output type pin. Used to build array types in a type expression
    /// (eg. a generic argument), as opposed to <see cref="MakeArrayNode"/>, which creates array
    /// values at runtime.
    /// </summary>
    [NodeKind(NodeKindIds.MakeArrayType)]
    public sealed partial class MakeArrayTypeNode : Node
    {
        private readonly ObservableValue<BaseType> arrayType = new(ArrayOf(TypeSpecifier.FromType<object>()));

        private partial NodeInputTypePin ElementTypePin { get; }

        [PinType(nameof(arrayType))]
        internal partial NodeOutputTypePin ArrayTypePin { get; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its element-type input type pin and
        /// its array-type output type pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public MakeArrayTypeNode(NodeGraph graph)
            : base(graph)
        {
            DeclarePins();
        }

        /// <summary>
        /// Recomputes the output array type from the input element type.
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);

            // Set the type of the output type pin by constructing
            // the type of this node with the input type pins.
            arrayType.Value = GetArrayType();
        }

        private BaseType GetArrayType() =>
            ArrayOf(ElementTypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>());

        private static TypeSpecifier ArrayOf(BaseType elementType) =>
            new(elementType.Name + "[]", false, false, null);

        /// <summary>
        /// Returns the array type's short name.
        /// </summary>
        /// <returns>The array type's short name.</returns>
        public override string ToString()
        {
            // Always assigned from GetArrayType(), which never returns null.
            return arrayType.RequireValue().ShortName;
        }
    }
}
