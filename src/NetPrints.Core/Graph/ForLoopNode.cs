#nullable enable
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an integer-based for-loop.
    /// </summary>
    [NodeKind(NodeKindIds.ForLoop)]
    public sealed partial class ForLoopNode : Node
    {
        /// <summary>
        /// Input execution pin that executes the loop.
        /// </summary>
        public partial NodeInputExecPin ExecPin { get; }

        /// <summary>
        /// Input execution pin that skips the current loop step.
        /// Should only be executed from within this loop.
        /// </summary>
        public partial NodeInputExecPin ContinuePin { get; }

        /// <summary>
        /// Execution pin that gets executed for each loop.
        /// </summary>
        public partial NodeOutputExecPin LoopPin { get; }

        /// <summary>
        /// Execution pin that gets executed when the loop is over.
        /// </summary>
        public partial NodeOutputExecPin CompletedPin { get; }

        /// <summary>
        /// Input data pin for the initial inclusive index value of the loop.
        /// </summary>
        public partial NodeInputDataPin<int> InitialIndexPin { get; }

        /// <summary>
        /// Input data pin for the maximum exclusive index value of the loop.
        /// </summary>
        public partial NodeInputDataPin<int> MaxIndexPin { get; }

        /// <summary>
        /// Output data pin for the current index value of the loop.
        /// Starts at the value of InitialIndexPin and increases up to,
        /// but not including, MaxIndexPin.
        /// </summary>
        public partial NodeOutputDataPin<int> IndexPin { get; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its execution, continue, index-range
        /// and index pins. Defaults <see cref="InitialIndexPin"/>'s unconnected value to 0.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public ForLoopNode(NodeGraph graph)
            : base(graph)
        {
            DeclarePins();

            MaxIndexPin.UnconnectedValue = null;
        }

        /// <summary>
        /// Returns "For Loop".
        /// </summary>
        /// <returns>"For Loop".</returns>
        public override string ToString()
        {
            return "For Loop";
        }

        /// <inheritdoc />
        public override bool TitleIsFixed => true;
    }
}
