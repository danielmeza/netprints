#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node that sets the value of a variable.
    /// </summary>
    [DataContract]
    public class VariableSetterNode : VariableNode
    {
        /// <summary>
        /// Input data pin for the new value of the variable.
        /// </summary>
        public NodeInputDataPin NewValuePin
        {
            get { return IsStatic ? InputDataPins[0] : InputDataPins[1]; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/>, builds its pins from <paramref name="variable"/>
        /// (see <see cref="VariableNode"/>'s constructor), and adds its execution pins and its new-value
        /// input data pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="variable">Specifier for the variable this node writes.</param>
        public VariableSetterNode(NodeGraph graph, VariableSpecifier variable)
            : base(graph, variable)
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Exec");

            AddInputDataPin("NewValue", variable.Type);
        }

        /// <summary>
        /// Returns "Set " followed by the declaring type's short name (for a static variable) and the
        /// variable's name.
        /// </summary>
        /// <returns>The node's display string.</returns>
        public override string ToString()
        {
            string staticText = IsStatic ? $"{TargetType.ShortName}." : "";
            return $"Set {staticText}{VariableName}";
        }
    }
}
