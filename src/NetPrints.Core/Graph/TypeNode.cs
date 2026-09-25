#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// A settable, observable value of (usually reference) type <typeparamref name="T"/>. Used for
    /// pin/node inferred and literal types, which can genuinely be unset (nothing connected yet), so
    /// <see cref="Value"/> is annotated as nullable for reference types via <see cref="AllowNullAttribute"/>/
    /// <see cref="MaybeNullAttribute"/> (an unconstrained type parameter has no nullable annotation of its
    /// own to express that directly).
    /// </summary>
    [DataContract]
    public class ObservableValue<T> : INotifyPropertyChanged
    {
        /// <summary>
        /// Raised by <see cref="OnValueChanged"/> after <see cref="Value"/> is set (unconditionally,
        /// even if the new value equals the old one).
        /// </summary>
        /// <param name="sender">The <see cref="ObservableValue{T}"/> whose value changed.</param>
        /// <param name="eventArgs">Always <see cref="EventArgs.Empty"/>.</param>
        public delegate void ObservableValueChangedEventHandler(object sender, EventArgs eventArgs);

        /// <summary>
        /// The current value. Setting it raises <see cref="OnValueChanged"/> and
        /// <see cref="PropertyChanged"/> unconditionally, even if the new value equals the old one.
        /// </summary>
        [DataMember]
        [AllowNull, MaybeNull]
        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                OnValueChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        [AllowNull, MaybeNull]
        private T value;

        /// <summary>
        /// Creates an observable value initialized to <paramref name="value"/>. Does not raise
        /// <see cref="OnValueChanged"/> or <see cref="PropertyChanged"/> for this initial value.
        /// </summary>
        /// <param name="value">Initial value.</param>
        public ObservableValue([AllowNull] T value)
        {
            this.value = value;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raised after <see cref="Value"/> is set.
        /// </summary>
        public event ObservableValueChangedEventHandler? OnValueChanged;

        /// <summary>
        /// Unwraps <paramref name="observableValue"/>'s current <see cref="Value"/>.
        /// </summary>
        /// <param name="observableValue">Observable value to unwrap.</param>
        /// <returns><paramref name="observableValue"/>'s <see cref="Value"/>.</returns>
        [return: MaybeNull]
        public static implicit operator T(ObservableValue<T> observableValue)
        {
            return observableValue.Value;
        }

        /// <summary>
        /// Wraps <paramref name="value"/> in a new observable value.
        /// </summary>
        /// <param name="value">Value to wrap.</param>
        /// <returns>A new <see cref="ObservableValue{T}"/> initialized to <paramref name="value"/>.</returns>
        public static implicit operator ObservableValue<T>([AllowNull] T value)
        {
            return new ObservableValue<T>(value);
        }
    }

    /// <summary>
    /// Pure type node that outputs a fixed type (with its generic arguments, if any, resolved through
    /// input type pins) as a single output type pin. Used to reference a type by name in a type
    /// expression.
    /// </summary>
    [DataContract]
    public partial class TypeNode : Node
    {
        /// <summary>
        /// The (possibly open generic) type this node is fixed to. Its resolved form, with generic
        /// arguments substituted by this node's input type pins, is exposed through the output type
        /// pin, not through this property directly.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial BaseType Type { get; private set; }

        [DataMember]
        private ObservableValue<BaseType> constructedType;

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it a generic-argument input type pin
        /// for each of <paramref name="type"/>'s generic arguments, plus its output type pin.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="type">The type this node references.</param>
        public TypeNode(NodeGraph graph, BaseType type)
            : base(graph)
        {
            Type = type;

            // Add type pins for each generic argument of the literal type
            // and monitor them for changes to reconstruct the actual pin types.
            if (Type is TypeSpecifier typeSpecifier)
            {
                foreach (var genericArg in typeSpecifier.GenericArguments.OfType<GenericType>())
                {
                    AddInputTypePin(genericArg.Name);
                }
            }

            constructedType = new ObservableValue<BaseType>(GetConstructedOutputType());
            AddOutputTypePin("OutputType", constructedType);
        }

        /// <summary>
        /// Reconstructs the output type pin's type from <see cref="Type"/> with its generic parameters
        /// substituted by this node's input type pins.
        /// </summary>
        /// <param name="sender">The node whose input type changed.</param>
        /// <param name="eventArgs">Unused; forwarded to the base implementation.</param>
        protected override void HandleInputTypeChanged(object? sender, EventArgs? eventArgs)
        {
            base.HandleInputTypeChanged(sender, eventArgs);

            // Set the type of the output type pin by constructing
            // the type of this node with the input type pins.
            constructedType.Value = GetConstructedOutputType();
        }

        private BaseType GetConstructedOutputType()
        {
            return GenericsHelper.ConstructWithTypePins(Type, InputTypePins);
        }

        /// <summary>
        /// Returns <see cref="Type"/>'s short name.
        /// </summary>
        /// <returns><see cref="Type"/>'s short name.</returns>
        public override string ToString()
        {
            return Type.ShortName;
        }
    }
}
