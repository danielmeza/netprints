#nullable enable
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for variable nodes.
    /// </summary>
    [DataContract]
    [KnownType(typeof(VariableGetterNode))]
    [KnownType(typeof(VariableSetterNode))]
    public abstract partial class VariableNode : Node
    {
        /// <summary>
        /// Target object of this variable node.
        /// Can be null for local variables.
        /// </summary>
        public NodeInputDataPin? TargetPin
        {
            get { return !IsLocalVariable && !IsStatic ? InputDataPins[0] : null; }
        }

        /// <summary>
        /// Pin that outputs the value of the variable.
        /// </summary>
        public NodeOutputDataPin ValuePin
        {
            get { return OutputDataPins[0]; }
        }

        /// <summary>
        /// Whether the variable is a local variable.
        /// </summary>
        public bool IsLocalVariable => TargetType is null;

        /// <summary>
        /// Name of this variable.
        /// </summary>
        public string VariableName { get => Variable.Name; }

        /// <summary>
        /// Specifier for the type of the target object.
        /// </summary>
        public TypeSpecifier TargetType { get => Variable.DeclaringType; }

        /// <summary>
        /// Whether the variable is static.
        /// </summary>
        public bool IsStatic
        {
            get => Variable.Modifiers.HasFlag(VariableModifiers.Static);
        }

        /// <summary>
        /// Whether this variable node is for an indexer (eg. dict["key"]).
        /// </summary>
        public bool IsIndexer
        {
            get => Variable.Name == "this[]";
        }

        /// <summary>
        /// Specifier for the type of the index.
        /// </summary>
        public BaseType? IndexType
        {
            // TODO: Get indexer type
            get => IsIndexer ? TypeSpecifier.FromType<object>() : null;
        }

        /// <summary>
        /// Data pin for the indexer.
        /// </summary>
        public NodeInputDataPin? IndexPin
        {
            get => IsIndexer ? InputDataPins[1] : null;
        }

        /// <summary>
        /// Specifier for the underlying variable.
        /// </summary>
        [ObservableProperty]
        [DataMember(Name = "FieldOrProperty")]
        public partial VariableSpecifier Variable { get; private set; }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and builds its pins from
        /// <paramref name="variable"/>: a target input pin unless it is local or static, an index
        /// input pin if it is an indexer, and the output data pin for its value.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="variable">Specifier for the variable this node accesses.</param>
        protected VariableNode(NodeGraph graph, VariableSpecifier variable)
            : base(graph)
        {
            Variable = variable;

            // Add target input pin if not local or static
            if (!IsLocalVariable && !Variable.Modifiers.HasFlag(VariableModifiers.Static))
            {
                AddInputDataPin("Target", TargetType);
            }

            if (IsIndexer)
            {
                // TODO: Get indexer type (matches IndexType's own placeholder above).
                AddInputDataPin("Index", TypeSpecifier.FromType<object>());
            }

            AddOutputDataPin(Variable.Type.ShortName, Variable.Type);
        }
    }
}
