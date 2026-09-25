#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing a reroute node. Does nothing by itself.
    /// Used for layouting in the editor.
    /// </summary>
    [DataContract]
    public class RerouteNode : Node
    {
        /// <summary>
        /// Number of execution pin pairs this node reroutes (0 unless created by <see cref="MakeExecution"/>).
        /// </summary>
        public int ExecRerouteCount { get => InputExecPins.Count; }

        /// <summary>
        /// Number of data pin pairs this node reroutes (0 unless created by <see cref="MakeData"/>).
        /// </summary>
        public int DataRerouteCount { get => InputDataPins.Count; }

        /// <summary>
        /// Number of type pin pairs this node reroutes (0 unless created by <see cref="MakeType"/>).
        /// </summary>
        public int TypeRerouteCount { get => InputTypePins.Count; }

        private RerouteNode(NodeGraph graph)
            : base(graph)
        {
        }

        /// <summary>
        /// Creates a reroute node with <paramref name="numExecs"/> input/output execution pin pairs
        /// (one incoming and one outgoing pin per pair, connected only when the caller connects them).
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="numExecs">Number of execution pin pairs to create.</param>
        /// <returns>The new reroute node.</returns>
        public static RerouteNode MakeExecution(NodeGraph graph, int numExecs)
        {
            var node = new RerouteNode(graph);

            for (int i = 0; i < numExecs; i++)
            {
                node.AddInputExecPin($"Exec{i}");
                node.AddOutputExecPin($"Exec{i}");
            }

            return node;
        }

        /// <summary>
        /// Creates a reroute node with one input/output data pin pair per entry of
        /// <paramref name="dataTypes"/> (each entry's input pin typed by its first element, output pin
        /// by its second).
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="dataTypes">Input/output type pair for each data pin pair to create.</param>
        /// <returns>The new reroute node.</returns>
        /// <exception cref="ArgumentException"><paramref name="dataTypes"/> is <see langword="null"/>.</exception>
        public static RerouteNode MakeData(NodeGraph graph, IEnumerable<Tuple<BaseType, BaseType>> dataTypes)
        {
            if (dataTypes is null)
            {
                throw new ArgumentException("dataTypes was null in RerouteNode.MakeData.");
            }

            var node = new RerouteNode(graph);

            int index = 0;
            foreach (var dataType in dataTypes)
            {
                node.AddInputDataPin($"Data{index}", dataType.Item1);
                node.AddOutputDataPin($"Data{index}", dataType.Item2);
                index++;
            }

            return node;
        }

        /// <summary>
        /// Creates a reroute node with <paramref name="numTypes"/> input/output type pin pairs, each
        /// output pin's inferred type initially unset.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="numTypes">Number of type pin pairs to create.</param>
        /// <returns>The new reroute node.</returns>
        public static RerouteNode MakeType(NodeGraph graph, int numTypes)
        {
            var node = new RerouteNode(graph);

            for (int i = 0; i < numTypes; i++)
            {
                node.AddInputTypePin($"Type{i}");
                node.AddOutputTypePin($"Type{i}", new ObservableValue<BaseType>(null));
            }

            return node;
        }

        private void UpdateOutputType()
        {
            if (OutputTypePins.Count > 0)
            {
                OutputTypePins[0].InferredType.Value = InputTypePins[0].InferredType?.Value;
            }
        }

        /// <summary>
        /// Propagates the input type pin's inferred type to the output type pin (a type reroute only).
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);
            UpdateOutputType();
        }

        /// <summary>
        /// Returns "Reroute".
        /// </summary>
        /// <returns>"Reroute".</returns>
        public override string ToString()
        {
            return "Reroute";
        }
    }
}
