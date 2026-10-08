#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for variable nodes.
    /// </summary>
    public abstract partial class VariableNode : Node, IMemberReferencingNode
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
        /// Whether the variable is a method-local variable (<see cref="VariableSpecifier.Scope"/> is
        /// <see cref="VariableScope.Local"/>, US5), rather than a class member.
        /// </summary>
        public bool IsLocalVariable => Variable.Scope == VariableScope.Local;

        /// <summary>
        /// Name of this variable.
        /// </summary>
        public string VariableName { get => Variable.Name; }

        /// <summary>
        /// Specifier for the type of the target object, or <see langword="null"/> for a method-local
        /// variable (<see cref="IsLocalVariable"/>).
        /// </summary>
        public TypeSpecifier? TargetType { get => Variable.DeclaringType; }

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

        /// <summary>
        /// Updates the variable this node accesses in place, keeping its pins and connections: a rename
        /// (US5) leaves the node's scope, declaring type, static-ness and value type untouched, so its
        /// pin shape does not change. A retype or a scope change replaces the node instead (its pin
        /// shape would differ), so this throws when <paramref name="variable"/> would change it.
        /// </summary>
        /// <param name="variable">The variable's new specifier.</param>
        /// <exception cref="ArgumentException"><paramref name="variable"/> would change the node's pin shape.</exception>
        public void Retarget(VariableSpecifier variable)
        {
            ArgumentNullException.ThrowIfNull(variable);

            if (variable.Scope != Variable.Scope
                || variable.DeclaringType != Variable.DeclaringType
                || variable.Type != Variable.Type
                || variable.Modifiers.HasFlag(VariableModifiers.Static) != IsStatic)
            {
                throw new ArgumentException("Retargeting a variable node must keep its pin shape (scope, declaring type, static-ness and value type); use a fresh node instead.", nameof(variable));
            }

            Variable = variable;
        }

        /// <inheritdoc />
        public bool RefersTo(MemberKey member) =>
            member.Kind == MemberKind.Variable
            && Variable.Scope == VariableScope.Member
            && Variable.Name == member.Name
            && Variable.DeclaringType == member.DeclaringType;

        /// <inheritdoc />
        public Action Retarget(MemberKey member, string newName)
        {
            VariableSpecifier before = Variable;
            Retarget(new VariableSpecifier(newName, before.Type, before.GetterVisibility, before.SetterVisibility, before.DeclaringType, before.Modifiers));
            return () => Retarget(before);
        }
    }
}
