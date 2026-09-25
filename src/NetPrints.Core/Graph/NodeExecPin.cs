#nullable enable
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for execution pins.
    /// </summary>
    [DataContract]
    public abstract class NodeExecPin : NodePin
    {
        /// <summary>
        /// Forwards to <see cref="NodePin"/>'s constructor; execution pins have no state of their own.
        /// </summary>
        /// <param name="node">Node the pin belongs to.</param>
        /// <param name="name">Name of the pin.</param>
        protected NodeExecPin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
