#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an if / else expression.
    /// </summary>
    [DataContract]
    public class IfElseNode : Node
    {
        /// <summary>
        /// Pin that gets executed if the statement was true.
        /// </summary>
        public NodeOutputExecPin TruePin
        {
            get { return OutputExecPins[0]; }
        }

        /// <summary>
        /// Pin that gets executed if the statement was false.
        /// </summary>
        public NodeOutputExecPin FalsePin
        {
            get { return OutputExecPins[1]; }
        }

        /// <summary>
        /// Input execution pin that executes this node.
        /// </summary>
        public NodeInputExecPin ExecutionPin
        {
            get { return InputExecPins[0]; }
        }

        /// <summary>
        /// Input data pin for the condition of this node.
        /// Expects a boolean value.
        /// </summary>
        public NodeInputDataPin ConditionPin
        {
            get { return InputDataPins[0]; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its execution, condition and
        /// true/false pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public IfElseNode(NodeGraph graph)
            : base(graph)
        {
            AddInputExecPin("Exec");

            AddInputDataPin("Condition", TypeSpecifier.FromType<bool>());

            AddOutputExecPin("True");
            AddOutputExecPin("False");
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
