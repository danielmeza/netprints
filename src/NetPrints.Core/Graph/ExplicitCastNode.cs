#nullable enable
using System;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing an explicit type cast.
    /// </summary>
    [DataContract]
    public class ExplicitCastNode : Node
    {
        /// <summary>
        /// Always <see langword="true"/>: casting alone is not considered a side effect worth
        /// sequencing, provided its failure/success branching is not needed (a pure cast node has no
        /// <see cref="CastSuccessPin"/>/<see cref="CastFailedPin"/> and throws on a failed cast).
        /// </summary>
        public override bool CanSetPure
        {
            get => true;
        }

        /// <summary>
        /// Pin for the object to cast to another type.
        /// </summary>
        public NodeInputDataPin ObjectToCast
        {
            get { return InputDataPins[0]; }
        }

        /// <summary>
        /// Input type pin for the type to cast to.
        /// </summary>
        public NodeInputTypePin CastTypePin
        {
            get { return InputTypePins[0]; }
        }

        /// <summary>
        /// Pin that holds the cast object.
        /// </summary>
        public NodeOutputDataPin CastPin
        {
            get { return OutputDataPins[0]; }
        }

        /// <summary>
        /// Pin that gets executed when the cast succeeded.
        /// </summary>
        public NodeOutputExecPin CastSuccessPin
        {
            get { return OutputExecPins[0]; }
        }

        /// <summary>
        /// Pin that gets executed when the cast failed.
        /// </summary>
        public NodeOutputExecPin CastFailedPin
        {
            get { return OutputExecPins[1]; }
        }

        /// <summary>
        /// Type to cast to. Inferred from input type pin.
        /// </summary>
        public BaseType CastType
        {
            get => CastTypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it its type, object, cast-result and
        /// exec pins.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        public ExplicitCastNode(NodeGraph graph)
            : base(graph)
        {
            AddInputTypePin("Type");
            AddInputDataPin("Object", TypeSpecifier.FromType<object>());
            AddOutputDataPin("CastObject", CastType);
            AddExecPins();
        }

        private void AddExecPins()
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Success");
            AddOutputExecPin("Failure");
        }

        /// <summary>
        /// Removes the success/failure exec pins and input exec pin when turned pure (disconnecting
        /// them first); restores them when turned impure.
        /// </summary>
        /// <param name="pure">The new purity value.</param>
        protected override void SetPurity(bool pure)
        {
            base.SetPurity(pure);

            if (pure)
            {
                var outExecPins = new NodeOutputExecPin[]
                {
                    OutputExecPins.Single(p => p.Name == "Success"),
                    OutputExecPins.Single(p => p.Name == "Failure"),
                };

                foreach (var execPin in outExecPins)
                {
                    GraphUtil.DisconnectOutputExecPin(execPin);
                    OutputExecPins.Remove(execPin);
                }

                var inExecPin = InputExecPins.Single(p => p.Name == "Exec");
                GraphUtil.DisconnectInputExecPin(inExecPin);
                InputExecPins.Remove(inExecPin);
            }
            else
            {
                AddExecPins();
            }
        }

        /// <summary>
        /// Propagates <see cref="CastType"/> (the inferred target type) to <see cref="CastPin"/>.
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);

            CastPin.PinType.Value = CastType;
        }

        /// <summary>
        /// Returns "Explicit Cast to " followed by the target type's short name.
        /// </summary>
        /// <returns>"Explicit Cast to " followed by the target type's short name.</returns>
        public override string ToString()
        {
            return $"Explicit Cast to {CastType.ShortName}";
        }
    }
}
