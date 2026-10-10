#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis.CSharp;
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

        private readonly List<BaseType> declaredTypes = [];

        private BaseType DeclaredType(int index) =>
            index < declaredTypes.Count ? declaredTypes[index] : TypeSpecifier.FromType<object>();

        /// <summary>
        /// Propagates each input type pin's inferred type (or the type declared for the argument, <see cref="object"/>
        /// by default, if none has been inferred) to the corresponding output data pin, so a custom event's argument pins (added by
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
                OutputDataPins[i].PinType.Value = InputTypePins[i].InferredType?.Value ?? DeclaredType(i);
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
        /// The entry's arguments, in order: the name and type of each output data pin. A pin whose type is not a
        /// plain <see cref="TypeSpecifier"/> reads as <see cref="object"/>.
        /// </summary>
        public IReadOnlyList<EventArgument> Arguments =>
            OutputDataPins.Select(pin => new EventArgument(pin.Name, pin.PinType.Value as TypeSpecifier ?? TypeSpecifier.FromType<object>())).ToList();

        /// <summary>
        /// The entry's arguments as declared: the name of each output data pin and the type set by
        /// <see cref="SetArguments"/> (<see cref="object"/> if none), not the type a connected type node infers.
        /// This is what is serialized, so a type node's own connection stays the only record of its type.
        /// </summary>
        public IReadOnlyList<EventArgument> DeclaredArguments =>
            OutputDataPins.Select((pin, i) => new EventArgument(pin.Name, DeclaredType(i) as TypeSpecifier ?? TypeSpecifier.FromType<object>())).ToList();

        /// <summary>
        /// Whether <paramref name="name"/> can name an argument: a C# identifier that is not a keyword.
        /// </summary>
        /// <param name="name">The candidate name.</param>
        /// <returns><see langword="true"/> if <see cref="SetArguments"/> accepts the name.</returns>
        public static bool IsValidArgumentName(string name) =>
            SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

        /// <summary>
        /// Replaces the custom event's arguments. The output pins follow in order: pins that stay keep their
        /// connections, extra ones are disconnected and removed, missing ones are added.
        /// </summary>
        /// <param name="arguments">The new arguments; names are valid C# identifiers and unique.</param>
        /// <exception cref="InvalidOperationException">This is an override entry, whose arguments come from the base method.</exception>
        /// <exception cref="ArgumentException">A name is not a valid identifier or is used twice; nothing changes.</exception>
        public void SetArguments(IReadOnlyList<EventArgument> arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            if (OverriddenMethod is not null)
            {
                throw new InvalidOperationException("An override entry's arguments come from the base method.");
            }

            HashSet<string> seen = [];
            foreach (EventArgument argument in arguments)
            {
                if (!IsValidArgumentName(argument.Name))
                {
                    throw new ArgumentException($"'{argument.Name}' is not a valid C# identifier", nameof(arguments));
                }

                if (!seen.Add(argument.Name))
                {
                    throw new ArgumentException($"The argument name '{argument.Name}' is used twice", nameof(arguments));
                }
            }

            while (OutputDataPins.Count > arguments.Count)
            {
                RemoveArgument();
            }

            while (OutputDataPins.Count < arguments.Count)
            {
                AddArgument();
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                OutputDataPins[i].Name = arguments[i].Name;
                OutputDataPins[i].PinType.Value = arguments[i].Type;
                declaredTypes[i] = arguments[i].Type;
            }
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
            declaredTypes.Add(TypeSpecifier.FromType<object>());
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
                NodeOutputDataPin odpToRemove = OutputDataPins[OutputDataPins.Count - 1];
                GraphUtil.DisconnectOutputDataPin(odpToRemove);
                OutputDataPins.Remove(odpToRemove);

                // An override entry's argument pins have no paired input type pin (their type is fixed
                // by the base signature); only remove one if this node actually has one to spare.
                if (InputTypePins.Count > 0)
                {
                    NodeInputTypePin itpToRemove = InputTypePins[InputTypePins.Count - 1];
                    GraphUtil.DisconnectInputTypePin(itpToRemove);
                    InputTypePins.Remove(itpToRemove);
                    declaredTypes.RemoveAt(declaredTypes.Count - 1);
                }
            }
        }
    }
}
