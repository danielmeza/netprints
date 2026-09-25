#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing a constructor call.
    /// </summary>
    [DataContract]
    public partial class ConstructorNode : ExecNode
    {
        /// <summary>
        /// Always <see langword="true"/>: a constructor node can become pure (no exec pins) since
        /// construction alone is not considered a side effect worth sequencing.
        /// </summary>
        public override bool CanSetPure
        {
            get => true;
        }

        /// <summary>
        /// Specifier for the constructor.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial ConstructorSpecifier ConstructorSpecifier { get; private set; }

        /// <summary>
        /// Specifier for the type this constructor creates.
        /// </summary>
        public BaseType ClassType
        {
            // Set from ConstructorSpecifier.DeclaringType when the output pin is created (below) and
            // never cleared, so it is never null.
            get => OutputDataPins[0].PinType.Value!;
        }

        /// <summary>
        /// List of type specifiers the constructor takes.
        /// </summary>
        public IReadOnlyList<BaseType> ArgumentTypes
        {
            // PinType.Value is set from ConstructorSpecifier.Arguments when the pin is created (below)
            // and never cleared, so it is never null for this node's own argument pins.
            get => ArgumentPins.Select(p => p.PinType.Value!).ToList();
        }

        /// <summary>
        /// List of node pins, one for each argument the constructor takes.
        /// </summary>
        public IList<NodeInputDataPin> ArgumentPins
        {
            get { return InputDataPins; }
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and builds its pins from
        /// <paramref name="specifier"/>: a generic-argument input type pin per generic argument of the
        /// constructed type, one input data pin per constructor argument, and the single output data
        /// pin for the constructed instance.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="specifier">Specifier for the constructor to call.</param>
        public ConstructorNode(NodeGraph graph, ConstructorSpecifier specifier)
            : base(graph)
        {
            ConstructorSpecifier = specifier;

            // Add type pins for each generic arguments of the type being constructed.
            foreach (var genericArg in ConstructorSpecifier.DeclaringType.GenericArguments.OfType<GenericType>())
            {
                AddInputTypePin(genericArg.Name);
            }

            foreach (Named<BaseType> argument in ConstructorSpecifier.Arguments)
            {
                AddInputDataPin(argument.Name, argument.Value);
            }

            AddOutputDataPin(ConstructorSpecifier.DeclaringType.ShortName, ConstructorSpecifier.DeclaringType);

            // TODO: Set the correct types to begin with.
            UpdateTypes();
        }

        /// <summary>
        /// Reconstructs every argument pin's type and the constructed-instance output pin's type from
        /// <see cref="ConstructorSpecifier"/> with its generic parameters substituted by this node's
        /// input type pins (<see cref="UpdateTypes"/>).
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);
            UpdateTypes();
        }

        private void UpdateTypes()
        {
            // Construct data input
            for (int i = 0; i < ConstructorSpecifier.Arguments.Count; i++)
            {
                BaseType type = ConstructorSpecifier.Arguments[i];

                // Construct type with generic arguments replaced by our input type pins
                BaseType constructedType = GenericsHelper.ConstructWithTypePins(type, InputTypePins);

                if (InputDataPins[i].PinType.Value != constructedType)
                {
                    InputDataPins[i].PinType.Value = constructedType;
                }
            }

            // Construct data output
            {
                BaseType constructedType = GenericsHelper.ConstructWithTypePins(ConstructorSpecifier.DeclaringType, InputTypePins);
                OutputDataPins[0].PinType.Value = constructedType;
            }
        }

        /// <summary>
        /// Returns "Construct " followed by the constructed type's short name.
        /// </summary>
        /// <returns>"Construct " followed by the constructed type's short name.</returns>
        public override string ToString()
        {
            return $"Construct {ClassType.ShortName}";
        }
    }
}
