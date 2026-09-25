#nullable enable
using System;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Pure type node producing the array type of its input element type (eg. <c>int</c> ->
    /// <c>int[]</c>), as a single output type pin. Used to build array types in a type expression
    /// (eg. a generic argument), as opposed to <see cref="MakeArrayNode"/>, which creates array
    /// values at runtime.
    /// </summary>
    [DataContract]
    public class MakeArrayTypeNode : Node
    {
        [DataMember]
        private ObservableValue<BaseType> arrayType;

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its element-type input type pin and
        /// its array-type output type pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public MakeArrayTypeNode(NodeGraph graph)
            : base(graph)
        {
            AddInputTypePin("ElementType");

            arrayType = new ObservableValue<BaseType>(GetArrayType());

            AddOutputTypePin("ArrayType", arrayType);
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

        private BaseType GetArrayType()
        {
            var elementType = InputTypePins[0].InferredType?.Value ?? TypeSpecifier.FromType<object>();
            return new TypeSpecifier(elementType.Name + "[]", false, false, null);
        }

        /// <summary>
        /// Returns the array type's short name.
        /// </summary>
        /// <returns>The array type's short name.</returns>
        public override string ToString()
        {
            // Always assigned from GetArrayType(), which never returns null.
            return arrayType.Value!.ShortName;
        }
    }
}
