#nullable enable
using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// One entry point of a class's <see cref="Core.EventGraph"/> (US4, data-model.md §4): a custom
    /// event (<see cref="EventEntryNode(Core.EventGraph, string)"/>, one exec output pin, no arguments
    /// initially) or an override of a base class method
    /// (<see cref="EventEntryNode(Core.EventGraph, MethodSpecifier)"/>, argument pins matching the base
    /// signature). Each entry of an event graph is translated into its own method, in node order.
    /// </summary>
    public sealed partial class EventEntryNode : Node
    {
        /// <summary>
        /// The event's name: a custom event's own name, or the overridden method's name. Names the
        /// generated method.
        /// </summary>
        [ObservableProperty]
        public partial string EventName { get; set; }

        /// <summary>
        /// Visibility of the generated method.
        /// </summary>
        [ObservableProperty]
        public partial MemberVisibility Visibility { get; set; }

        /// <summary>
        /// Modifiers of the generated method (<see cref="MethodModifiers.Async"/>,
        /// <see cref="MethodModifiers.Static"/> and <see cref="MethodModifiers.Override"/> are
        /// meaningful here). An override entry always has <see cref="MethodModifiers.Override"/> set.
        /// </summary>
        [ObservableProperty]
        public partial MethodModifiers Modifiers { get; set; }

        /// <summary>
        /// The base method this entry overrides, or <see langword="null"/> for a custom event.
        /// </summary>
        public MethodSpecifier? OverriddenMethod { get; }

        /// <summary>
        /// Output execution pin that initially executes when the event fires.
        /// </summary>
        public NodeOutputExecPin InitialExecutionPin => OutputExecPins[0];

        /// <summary>
        /// Creates a custom event entry: one output execution pin, no arguments.
        /// </summary>
        /// <param name="graph">Event graph the node belongs to.</param>
        /// <param name="eventName">Name of the custom event, and of the generated method.</param>
        public EventEntryNode(Core.EventGraph graph, string eventName)
            : base(graph)
        {
            ArgumentNullException.ThrowIfNull(eventName);

            AddOutputExecPin("Exec");
            EventName = eventName;
            Visibility = MemberVisibility.Public;
        }

        /// <summary>
        /// Creates an override entry: one output execution pin, and one output data pin per parameter
        /// of <paramref name="overridden"/>, typed and named from its signature (fixed; unlike a custom
        /// event's arguments, not backed by a paired input type pin).
        /// </summary>
        /// <param name="graph">Event graph the node belongs to.</param>
        /// <param name="overridden">The base method this entry overrides.</param>
        public EventEntryNode(Core.EventGraph graph, MethodSpecifier overridden)
            : base(graph)
        {
            ArgumentNullException.ThrowIfNull(overridden);

            AddOutputExecPin("Exec");
            EventName = overridden.Name;
            Visibility = overridden.Visibility;
            Modifiers = MethodModifiers.Override;
            OverriddenMethod = overridden;

            foreach (MethodParameter parameter in overridden.Parameters)
            {
                AddOutputDataPin(parameter.Name, new ObservableValue<BaseType>(parameter.Value));
            }
        }

        /// <summary>
        /// Propagates each input type pin's inferred type (or <see cref="object"/> if none has been
        /// inferred) to the corresponding output data pin, so a custom event's argument pins (added by
        /// <see cref="AddArgument"/>) reflect a connected type. An override entry's argument pins have
        /// no input type pins and are unaffected.
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
        /// Returns <see cref="EventName"/> followed by " Entry".
        /// </summary>
        /// <returns><see cref="EventName"/> followed by " Entry".</returns>
        public override string ToString() => $"{EventName} Entry";

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
        /// Adds one more custom-event argument: an output data pin typed <see cref="object"/> by
        /// default, and the matching input type pin used to resolve its actual type from a connected
        /// type node (the same pin pattern as <see cref="MethodEntryNode.AddArgument"/>).
        /// </summary>
        public void AddArgument()
        {
            int argIndex = OutputDataPins.Count;
            AddOutputDataPin($"Input{argIndex}", new ObservableValue<BaseType>(TypeSpecifier.FromType<object>()));
            AddInputTypePin($"Input{argIndex}Type");
        }

        /// <summary>
        /// Removes the last argument added by <see cref="AddArgument"/> (its output data pin and, if
        /// present, its input type pin), disconnecting them first. Does nothing if there are no
        /// arguments.
        /// </summary>
        public void RemoveArgument()
        {
            if (OutputDataPins.Count > 0)
            {
                NodeOutputDataPin odpToRemove = OutputDataPins.Last();
                GraphUtil.DisconnectOutputDataPin(odpToRemove);
                OutputDataPins.Remove(odpToRemove);

                // An override entry's argument pins have no paired input type pin (their type is fixed
                // by the base signature); only remove one if this node actually has one to spare.
                if (InputTypePins.Count > 0)
                {
                    NodeInputTypePin itpToRemove = InputTypePins.Last();
                    GraphUtil.DisconnectInputTypePin(itpToRemove);
                    InputTypePins.Remove(itpToRemove);
                }
            }
        }
    }
}
