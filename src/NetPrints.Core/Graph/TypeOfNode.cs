#nullable enable
using System;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing a C# <c>typeof</c> expression: outputs the runtime <see cref="Type"/> for its
    /// input type pin.
    /// </summary>
    [NodeKind("typeOf")]
    public sealed partial class TypeOfNode : Node
    {
        /// <summary>
        /// Input type pin for the Type value.
        /// </summary>
        [Pin("Type")]
        public partial NodeInputTypePin InputTypePin { get; }

        /// <summary>
        /// Output data pin for the Type value.
        /// </summary>
        public partial NodeOutputDataPin<Type> TypePin { get; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its input type pin and its
        /// <see cref="Type"/>-valued output pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public TypeOfNode(NodeGraph graph)
            : base(graph)
        {
            DeclarePins();
        }

        /// <summary>
        /// Returns "Type Of".
        /// </summary>
        /// <returns>"Type Of".</returns>
        public override string ToString()
        {
            return $"Type Of";
        }
    }
}
