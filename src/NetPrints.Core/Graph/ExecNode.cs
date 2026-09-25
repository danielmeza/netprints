#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for nodes that can be executed.
    /// </summary>
    [DataContract]
    [KnownType(typeof(CallMethodNode))]
    [KnownType(typeof(ConstructorNode))]
    public abstract class ExecNode : Node
    {
        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it one input and one output
        /// execution pin, both named "Exec".
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        protected ExecNode(NodeGraph graph)
            : base(graph)
        {
            AddExecPins();
        }

        private void AddExecPins()
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Exec");
        }

        /// <summary>
        /// Removes the node's exec pins (disconnecting them first) when turned pure, or restores them
        /// when turned impure.
        /// </summary>
        /// <param name="pure">The new purity value.</param>
        protected override void SetPurity(bool pure)
        {
            base.SetPurity(pure);

            if (pure)
            {
                GraphUtil.DisconnectInputExecPin(InputExecPins[0]);
                InputExecPins.RemoveAt(0);

                GraphUtil.DisconnectOutputExecPin(OutputExecPins[0]);
                OutputExecPins.RemoveAt(0);
            }
            else if (!pure)
            {
                AddExecPins();
            }
        }
    }
}
