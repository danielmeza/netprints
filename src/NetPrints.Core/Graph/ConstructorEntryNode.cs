#nullable enable
using System;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing the initial execution node of a constructor.
    /// </summary>
    [DataContract]
    public class ConstructorEntryNode : ExecutionEntryNode
    {
        /// <summary>
        /// Same as the base <see cref="Node.Graph"/>, but typed as <see cref="ConstructorGraph"/>:
        /// the constructor only accepts one, so <see cref="Node.Graph"/> is always one for a
        /// <see cref="ConstructorEntryNode"/>.
        /// </summary>
        public ConstructorGraph ConstructorGraph
        {
            get => (ConstructorGraph)Graph;
        }

        /// <summary>
        /// Adds this node to <paramref name="constructor"/> and gives it its single output execution
        /// pin.
        /// </summary>
        /// <param name="constructor">Constructor graph the node belongs to.</param>
        public ConstructorEntryNode(ConstructorGraph constructor)
            : base(constructor)
        {
            AddOutputExecPin("Exec");

            // TODO: Add output data and type pins for constructor graph
        }

        /// <summary>
        /// Returns the declaring class's name (or "?" if the graph has no class yet) followed by
        /// " Constructor Entry".
        /// </summary>
        /// <returns>The declaring class's name, or "?", followed by " Constructor Entry".</returns>
        public override string ToString()
        {
            return $"{ConstructorGraph.Class?.Name ?? "?"} Constructor Entry";
        }

        /// <summary>
        /// For an argument pin (an output data pin of this node), returns <c>"Input&lt;i&gt;"</c>
        /// (<paramref name="pin"/>'s position among <see cref="Node.OutputDataPins"/>), so a user
        /// rename of the argument does not change its pin key (document-format.md §1.4.2). Every
        /// other pin uses the base <see cref="Node.GetPinKeyName"/>.
        /// </summary>
        /// <param name="pin">Pin of this node to get the key name of.</param>
        /// <returns>The pin's keyName.</returns>
        public override string GetPinKeyName(NodePin pin)
        {
            if (pin is NodeOutputDataPin outputDataPin)
            {
                int index = OutputDataPins.IndexOf(outputDataPin);
                if (index >= 0)
                {
                    return $"Input{index}";
                }
            }

            return base.GetPinKeyName(pin);
        }
    }
}
