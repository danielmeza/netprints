#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Abstract base class for graphs with a body of executable nodes: <see cref="MethodGraph"/> and
    /// <see cref="ConstructorGraph"/>. Holds the single <see cref="EntryNode"/> execution starts from,
    /// the graph's argument types (derived from the entry node's pins), its visibility and its
    /// method-local variables (US5). Raises <see cref="System.ComponentModel.INotifyPropertyChanged"/>
    /// notifications through CommunityToolkit.Mvvm's generated <c>[INotifyPropertyChanged]</c>
    /// boilerplate (it cannot inherit <see cref="ModelObject"/>: it already inherits
    /// <see cref="NodeGraph"/>).
    /// </summary>
    [INotifyPropertyChanged]
    public abstract partial class ExecutionGraph : NodeGraph
    {
        /// <summary>
        /// Variables local to this method or constructor (US5, data-model.md §3), declared at the top
        /// of the generated body in collection order.
        /// </summary>
        public ObservableRangeCollection<LocalVariable> LocalVariables { get; private set; } = new();

        /// <summary>
        /// Whether <paramref name="name"/> is free to use for a local of this graph: not one of this
        /// graph's parameter names, not another local's name (<paramref name="except"/> is excluded
        /// from that check, so a variable can keep its own name while being renamed to something else
        /// and back), and a valid C# identifier that is not a reserved keyword.
        /// </summary>
        /// <param name="name">Candidate local variable name.</param>
        /// <param name="except">A local to exclude from the "not another local" check (its own current
        /// name, when checking a candidate rename for it), or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="name"/> is available.</returns>
        public bool IsLocalNameAvailable(string name, LocalVariable? except = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            return SyntaxFacts.IsValidIdentifier(name)
                && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
                && !NamedArgumentTypes.Any(argument => argument.Name == name)
                && !LocalVariables.Any(local => local != except && local.Name == name);
        }

        /// <summary>
        /// Entry node where execution starts. Always set by the concrete subclass's constructor
        /// (<see cref="MethodGraph"/>, <see cref="ConstructorGraph"/>) as its first statement, before
        /// anything else can observe the graph; reading it earlier throws.
        /// </summary>
        public ExecutionEntryNode EntryNode
        {
            get => entryNode ?? throw new InvalidOperationException(
                $"{GetType().Name}.EntryNode was read before it was set.");
            protected set => entryNode = value;
        }

        private ExecutionEntryNode? entryNode;

        /// <summary>
        /// Ordered argument types this graph takes.
        /// </summary>
        public IEnumerable<BaseType> ArgumentTypes
        {
            get => EntryNode.InputTypePins.Select(pin => pin.InferredType?.Value ?? TypeSpecifier.FromType<object>()).ToList();
        }

        /// <summary>
        /// Ordered argument types with their names this graph takes.
        /// </summary>
        public IEnumerable<Named<BaseType>> NamedArgumentTypes
        {
            get => EntryNode.InputTypePins.Zip(EntryNode.OutputDataPins, (type, data) => (type, data))
                .Select(pair => new Named<BaseType>(pair.data.Name, pair.type.InferredType?.Value ?? TypeSpecifier.FromType<object>())).ToList();
        }

        /// <summary>
        /// Visibility of this graph.
        /// </summary>
        [ObservableProperty]
        public partial MemberVisibility Visibility { get; set; } = MemberVisibility.Private;
    }
}
