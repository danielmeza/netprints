#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Abstract base class for graphs with a body of executable nodes: <see cref="MethodGraph"/> and
    /// <see cref="ConstructorGraph"/>. Holds the single <see cref="EntryNode"/> execution starts from,
    /// the graph's argument types (derived from the entry node's pins) and its visibility.
    /// </summary>
    [DataContract]
    public abstract class ExecutionGraph : NodeGraph
    {
        /// <summary>
        /// Entry node where execution starts. Always set by the concrete subclass's constructor
        /// (<see cref="MethodGraph"/>, <see cref="ConstructorGraph"/>) as its first statement, before
        /// anything else can observe the graph; the backing field stays nullable only because
        /// DataContract deserialization sets it through this property, bypassing constructors.
        /// </summary>
        [DataMember]
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
        [DataMember]
        public MemberVisibility Visibility
        {
            get;
            set;
        } = MemberVisibility.Private;
    }
}
