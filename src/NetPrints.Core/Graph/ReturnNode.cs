#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Represents a node which returns from a method.
    /// </summary>
    [DataContract]
    public class ReturnNode : Node
    {
        /// <summary>
        /// Execution pin that returns from the method when executed.
        /// </summary>
        public NodeInputExecPin ReturnPin
        {
            get { return InputExecPins[0]; }
        }

        /// <summary>
        /// Same as the base <see cref="Node.MethodGraph"/>, but non-nullable: the constructor only
        /// accepts a <see cref="Core.MethodGraph"/>, so <see cref="Node.Graph"/> is always one for a
        /// <see cref="ReturnNode"/>. Computed from <see cref="Node.Graph"/> on every access instead of
        /// cached in a field set by the constructor: DataContract deserialization bypasses
        /// constructors entirely and sets <see cref="Node.Graph"/> directly, so a cached field would
        /// stay null after loading a saved project.
        /// </summary>
        private MethodGraph methodGraph => (MethodGraph)Graph;

        public ReturnNode(MethodGraph graph)
            : base(graph)
        {
            AddInputExecPin("Exec");

            SetupSecondaryNodeEvents();
        }

        /// <summary>
        /// Sets the data pin types to the same as the main nodes' data pin types.
        /// </summary>
        private void ReplicateMainNodeInputTypes()
        {
            if (this == methodGraph.MainReturnNode)
            {
                return;
            }

            // Get new return types
            NodeInputDataPin[] mainInputPins = methodGraph.MainReturnNode.InputDataPins.ToArray();

            var oldConnections = new Dictionary<int, NodeOutputDataPin>();

            // Remember pins with same type as before
            foreach (NodeInputDataPin pin in InputDataPins)
            {
                int i = InputDataPins.IndexOf(pin);
                if (i < mainInputPins.Length && pin.IncomingPin != null)
                {
                    oldConnections.Add(i, pin.IncomingPin);
                }

                GraphUtil.DisconnectInputDataPin(pin);
            }

            InputDataPins.Clear();

            foreach (NodeInputDataPin mainInputPin in mainInputPins)
            {
                AddInputDataPin(mainInputPin.Name, mainInputPin.PinType.Value);
            }

            // Restore old connections
            foreach (var oldConn in oldConnections)
            {
                GraphUtil.ConnectDataPins(oldConn.Value, InputDataPins[oldConn.Key]);
            }
        }

        /// <summary>
        /// Sets the data pin types to the input type pin types.
        /// </summary>
        private void UpdateMainNodeInputTypes()
        {
            if (this != methodGraph.MainReturnNode)
            {
                return;
            }

            for (int i = 0; i < InputTypePins.Count; i++)
            {
                InputDataPins[i].PinType.Value = InputTypePins[i].InferredType?.Value ?? TypeSpecifier.FromType<object>();
            }
        }

        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);
            UpdateMainNodeInputTypes();
        }

        public void AddReturnType()
        {
            if (this != methodGraph.MainReturnNode)
            {
                throw new InvalidOperationException("Can only add return types on the main return node.");
            }

            int returnIndex = InputDataPins.Count;

            AddInputDataPin($"Output{returnIndex}", new ObservableValue<BaseType>(TypeSpecifier.FromType<object>()));
            AddInputTypePin($"Output{returnIndex}Type");
        }

        public void RemoveReturnType()
        {
            if (this != methodGraph.MainReturnNode)
            {
                throw new InvalidOperationException("Can only remove return types on the main return node.");
            }

            if (InputDataPins.Count > 0)
            {
                NodeInputDataPin idpToRemove = InputDataPins.Last();
                NodeInputTypePin itpToRemove = InputTypePins.Last();

                GraphUtil.DisconnectInputDataPin(idpToRemove);
                GraphUtil.DisconnectInputTypePin(itpToRemove);

                InputDataPins.Remove(idpToRemove);
                InputTypePins.Remove(itpToRemove);
            }
        }

        private void SetupSecondaryNodeEvents()
        {
            if (this == methodGraph.MainReturnNode)
            {
                UpdateMainNodeInputTypes();
            }
            else
            {
                methodGraph.MainReturnNode.InputDataPins.CollectionChanged += (sender, e) => ReplicateMainNodeInputTypes();
                methodGraph.MainReturnNode.InputTypeChanged += (sender, e) => ReplicateMainNodeInputTypes();
                ReplicateMainNodeInputTypes();
            }
        }

        public override string ToString()
        {
            return "Return";
        }
    }
}
