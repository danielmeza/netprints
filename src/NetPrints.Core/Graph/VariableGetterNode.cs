#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node that gets the value of a variable.
    /// </summary>
    [DataContract]
    public class VariableGetterNode : VariableNode
    {
        /// <summary>
        /// Adds this node to <paramref name="graph"/> and builds its pins from
        /// <paramref name="variable"/> (see <see cref="VariableNode"/>'s constructor).
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="variable">Specifier for the variable this node reads.</param>
        public VariableGetterNode(NodeGraph graph, VariableSpecifier variable)
            : base(graph, variable)
        {
        }

        /// <summary>
        /// Returns "Get " followed by the declaring type's short name (for a static variable) and the
        /// variable's name.
        /// </summary>
        /// <returns>The node's display string.</returns>
        public override string ToString()
        {
            string staticText = IsStatic ? $"{TargetType.ShortName}." : "";
            return $"Get {staticText}{VariableName}";
        }
    }
}
