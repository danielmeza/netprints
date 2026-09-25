#nullable enable
using System;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an exception throw.
    /// </summary>
    [DataContract]
    public class ThrowNode : Node
    {
        /// <summary>
        /// Pin for the exception to throw.
        /// </summary>
        public NodeInputDataPin ExceptionPin
        {
            get { return InputDataPins[0]; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its execution and exception pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public ThrowNode(NodeGraph graph)
            : base(graph)
        {
            AddInputExecPin("Exec");
            AddInputDataPin("Exception", TypeSpecifier.FromType<Exception>());
        }

        /// <summary>
        /// Returns "Throw Exception".
        /// </summary>
        /// <returns>"Throw Exception".</returns>
        public override string ToString()
        {
            return $"Throw Exception";
        }
    }
}
