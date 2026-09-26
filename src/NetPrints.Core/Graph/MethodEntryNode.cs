#nullable enable
using System;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing the initial execution node of a method.
    /// </summary>
    [DataContract]
    public class MethodEntryNode : ExecutionEntryNode
    {
        /// <summary>
        /// Same as the base <see cref="Node.MethodGraph"/>, but non-nullable: the constructor only
        /// accepts a <see cref="Core.MethodGraph"/>, so <see cref="Node.Graph"/> is always one for a
        /// <see cref="MethodEntryNode"/>. Computed from <see cref="Node.Graph"/> on every access
        /// instead of cached in a field set by the constructor: DataContract deserialization bypasses
        /// constructors entirely and sets <see cref="Node.Graph"/> directly, so a cached field would
        /// stay null after loading a saved project.
        /// </summary>
        private MethodGraph methodGraph => (MethodGraph)Graph;

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its single output execution pin.
        /// </summary>
        /// <param name="graph">Method graph the node belongs to.</param>
        public MethodEntryNode(MethodGraph graph)
            : base(graph)
        {
            AddOutputExecPin("Exec");
        }

        /// <summary>
        /// Propagates each input type pin's inferred type (or <see cref="object"/> if none has been
        /// inferred) to the corresponding output data pin, so a generic method's parameter pins
        /// reflect the resolved generic argument types.
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);

            for (int i = 0; i < InputTypePins.Count; i++)
            {
                OutputDataPins[i].PinType.Value = InputTypePins[i].InferredType?.Value ?? TypeSpecifier.FromType<object>();
            }
        }

        /// <summary>
        /// Returns the declaring method's name followed by " Entry".
        /// </summary>
        /// <returns>The declaring method's name followed by " Entry".</returns>
        public override string ToString()
        {
            return $"{methodGraph.Name} Entry";
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

        /// <summary>
        /// Adds one more parameter: an output data pin typed <see cref="object"/> by default, and the
        /// matching input type pin used to resolve its actual type from a generic argument.
        /// </summary>
        public void AddArgument()
        {
            int argIndex = OutputDataPins.Count;
            AddOutputDataPin($"Input{argIndex}", new ObservableValue<BaseType>(TypeSpecifier.FromType<object>()));
            AddInputTypePin($"Input{argIndex}Type");
        }

        /// <summary>
        /// Removes the last parameter added by <see cref="AddArgument"/> (its output data pin and
        /// input type pin), disconnecting them first. Does nothing if there are no parameters.
        /// </summary>
        public void RemoveArgument()
        {
            if (OutputDataPins.Count > 0)
            {
                NodeOutputDataPin odpToRemove = OutputDataPins.Last();
                NodeInputTypePin itpToRemove = InputTypePins.Last();

                GraphUtil.DisconnectOutputDataPin(odpToRemove);
                GraphUtil.DisconnectInputTypePin(itpToRemove);

                OutputDataPins.Remove(odpToRemove);
                InputTypePins.Remove(itpToRemove);
            }
        }

        /// <summary>
        /// Adds one more generic type parameter to the method: an output type pin named "T0", "T1",
        /// etc. and carrying a fresh <see cref="GenericType"/>.
        /// </summary>
        public void AddGenericArgument()
        {
            string name = $"T{OutputTypePins.Count}";
            AddOutputTypePin(name, new GenericType(name));
        }

        /// <summary>
        /// Removes the last generic type parameter added by <see cref="AddGenericArgument"/>,
        /// disconnecting its output type pin first. Does nothing if there are none.
        /// </summary>
        public void RemoveGenericArgument()
        {
            if (OutputTypePins.Count > 0)
            {
                NodeOutputTypePin otpToRemove = OutputTypePins.Last();

                GraphUtil.DisconnectOutputTypePin(otpToRemove);

                OutputTypePins.Remove(otpToRemove);
            }
        }
    }
}
