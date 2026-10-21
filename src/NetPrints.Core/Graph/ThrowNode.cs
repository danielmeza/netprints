#nullable enable
using System;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an exception throw.
    /// </summary>
    [NodeKind(NodeKindIds.Throw)]
    public sealed partial class ThrowNode : Node
    {
        /// <summary>
        /// Input execution pin that throws when executed.
        /// </summary>
        internal partial NodeInputExecPin ExecPin { get; }

        /// <summary>
        /// Pin for the exception to throw.
        /// </summary>
        public partial NodeInputDataPin<Exception> ExceptionPin { get; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its execution and exception pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public ThrowNode(NodeGraph graph)
            : base(graph)
        {
            DeclarePins();
        }

        /// <summary>
        /// Returns "Throw Exception".
        /// </summary>
        /// <returns>"Throw Exception".</returns>
        public override string ToString()
        {
            return $"Throw Exception";
        }

        /// <inheritdoc />
        public override bool TitleIsFixed => true;
    }
}
