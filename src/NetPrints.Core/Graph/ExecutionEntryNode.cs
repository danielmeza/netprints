#nullable enable
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract base class for the single entry node every <see cref="ExecutionGraph"/> starts with
    /// (see <see cref="ExecutionGraph.EntryNode"/>): <see cref="MethodEntryNode"/> for a method,
    /// <see cref="ConstructorEntryNode"/> for a constructor.
    /// </summary>
    [DataContract]
    public abstract class ExecutionEntryNode : Node
    {
        /// <summary>
        /// Output execution pin that initially executes when a method gets called.
        /// </summary>
        public NodeOutputExecPin InitialExecutionPin
        {
            get { return OutputExecPins[0]; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/>.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public ExecutionEntryNode(ExecutionGraph graph)
            : base(graph)
        {

        }
    }
}
