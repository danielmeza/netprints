#nullable enable
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an if / else expression.
    /// </summary>
    [NodeKind("ifElse")]
    public sealed partial class IfElseNode : Node
    {
        /// <summary>
        /// Input execution pin that executes this node.
        /// </summary>
        public partial NodeInputExecPin ExecPin { get; }

        /// <summary>
        /// Input data pin for the condition of this node.
        /// Expects a boolean value.
        /// </summary>
        public partial NodeInputDataPin<bool> ConditionPin { get; }

        /// <summary>
        /// Pin that gets executed if the statement was true.
        /// </summary>
        public partial NodeOutputExecPin TruePin { get; }

        /// <summary>
        /// Pin that gets executed if the statement was false.
        /// </summary>
        public partial NodeOutputExecPin FalsePin { get; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its execution, condition and
        /// true/false pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public IfElseNode(NodeGraph graph)
            : base(graph)
        {
            DeclarePins();
            ConditionPin.UnconnectedValue = null;
        }

        /// <summary>
        /// Returns "If Else".
        /// </summary>
        /// <returns>"If Else".</returns>
        public override string ToString()
        {
            return "If Else";
        }
    }
}
