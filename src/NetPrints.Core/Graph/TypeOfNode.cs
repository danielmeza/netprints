#nullable enable
using System;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing a C# <c>typeof</c> expression: outputs the runtime <see cref="Type"/> for its
    /// input type pin.
    /// </summary>
    [DataContract]
    public class TypeOfNode : Node
    {
        /// <summary>
        /// Output data pin for the Type value.
        /// </summary>
        public NodeOutputDataPin TypePin
        {
            get { return OutputDataPins[0]; }
        }

        /// <summary>
        /// Input type pin for the Type value.
        /// </summary>
        public NodeInputTypePin InputTypePin
        {
            get { return InputTypePins[0]; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its input type pin and its
        /// <see cref="Type"/>-valued output pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public TypeOfNode(NodeGraph graph)
            : base(graph)
        {
            AddInputTypePin("Type");
            AddOutputDataPin("Type", TypeSpecifier.FromType<Type>());
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
