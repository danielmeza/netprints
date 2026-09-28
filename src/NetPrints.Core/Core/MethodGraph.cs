#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Modifiers a method can have. Can be combined.
    /// </summary>
    [Flags]
    public enum MethodModifiers
    {
        /// <summary>
        /// No modifiers.
        /// </summary>
        None = 0,

        /// <summary>
        /// The method is sealed: an override that cannot be further overridden (C# <c>sealed</c>).
        /// </summary>
        Sealed = 8,

        /// <summary>
        /// The method has no body and must be overridden (C# <c>abstract</c>).
        /// </summary>
        Abstract = 16,

        /// <summary>
        /// The method is static rather than an instance member.
        /// </summary>
        Static = 32,

        /// <summary>
        /// The method can be overridden by a derived class (C# <c>virtual</c>).
        /// </summary>
        Virtual = 64,

        /// <summary>
        /// The method overrides a virtual or abstract base member (C# <c>override</c>).
        /// </summary>
        Override = 128,

        /// <summary>
        /// The method is asynchronous (C# <c>async</c>): the translator emits an <c>async</c> method
        /// and its <see cref="Graph.AwaitNode"/>s emit <c>await</c> expressions.
        /// </summary>
        Async = 256,
    }

    /// <summary>
    /// Method type. Contains common things usually associated with methods such as its arguments and its name.
    /// </summary>
    public partial class MethodGraph : ExecutionGraph
    {
        /// <summary>
        /// Return node of this method that when executed will return from the method.
        /// </summary>
        public IEnumerable<ReturnNode> ReturnNodes
        {
            get => Nodes.OfType<ReturnNode>();
        }

        /// <summary>
        /// Main return node that determines the return types of all other return nodes.
        /// </summary>
        public ReturnNode MainReturnNode
        {
            // Every MethodGraph is constructed with exactly one ReturnNode (below) and nothing removes
            // the last one (RemoveReturnType only removes return *values*, not the node); First() turns
            // a violation of that invariant into a clear exception instead of a null MainReturnNode.
            get => Nodes.OfType<ReturnNode>().First();
        }

        /// <summary>
        /// Ordered return types this method returns.
        /// </summary>
        public IEnumerable<BaseType> ReturnTypes
        {
            get => MainReturnNode.InputTypePins.Select(pin => pin.InferredType?.Value ?? TypeSpecifier.FromType<object>()).ToList();
        }

        /// <summary>
        /// Generic type arguments of the method.
        /// </summary>
        public IEnumerable<GenericType> GenericArgumentTypes
        {
            // NodeOutputTypePin.InferredType is non-nullable (its inferred type is never absent).
            get => EntryNode.OutputTypePins.Select(pin => pin.InferredType.Value).Cast<GenericType>().ToList();
        }

        /// <summary>
        /// Name of the method without any prefixes.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// Modifiers this method has.
        /// </summary>
        [ObservableProperty]
        public partial MethodModifiers Modifiers { get; set; } = MethodModifiers.None;

        /// <summary>
        /// Method entry node where this method graph's execution starts.
        /// </summary>
        public MethodEntryNode MethodEntryNode
        {
            get => (MethodEntryNode)EntryNode;
        }

        /// <summary>
        /// This method's member id (data-model.md §2), used as its graph key. Assigned once, in the
        /// constructor, from <see cref="IdGeneration.Current"/>; the mapper overwrites it from the
        /// document.
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// Creates a method given its name.
        /// </summary>
        /// <param name="name">Name for the method.</param>
        public MethodGraph(string name)
        {
            Id = IdGeneration.Current.NewId('m');
            Name = name;
            EntryNode = new MethodEntryNode(this);
            new ReturnNode(this);
        }

        /// <summary>
        /// Returns <see cref="Name"/>.
        /// </summary>
        /// <returns><see cref="Name"/>.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
