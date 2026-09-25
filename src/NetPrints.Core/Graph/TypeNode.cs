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
        public delegate void ObservableValueChangedEventHandler(object sender, EventArgs eventArgs);

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

        public ObservableValue([AllowNull] T value)
        {
            this.value = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event ObservableValueChangedEventHandler? OnValueChanged;

        [return: MaybeNull]
        public static implicit operator T(ObservableValue<T> observableValue)
        {
            return observableValue.Value;
        }

        public static implicit operator ObservableValue<T>([AllowNull] T value)
        {
            return new ObservableValue<T>(value);
        }
    }

    [DataContract]
    public partial class TypeNode : Node
    {
        [ObservableProperty]
        [DataMember]
        public partial BaseType Type { get; private set; }

        [DataMember]
        private ObservableValue<BaseType> constructedType;

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

        public override string ToString()
        {
            return Type.ShortName;
        }
    }
}
