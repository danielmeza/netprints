#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Type graph that returns a type.
    /// </summary>
    [DataContract]
    public class TypeGraph : NodeGraph
    {
        /// <summary>
        /// Return node of this type graph that receives the type.
        /// </summary>
        public TypeReturnNode ReturnNode
        {
            get => Nodes.OfType<TypeReturnNode>().Single();
        }

        /// <summary>
        /// TypeSpecifier for the type this graph returns.
        /// </summary>
        public TypeSpecifier ReturnType
        {
            get => (TypeSpecifier?)ReturnNode.TypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        /// <summary>
        /// The class this type graph belongs to, when it is a <see cref="Variable"/>'s
        /// <see cref="Variable.TypeGraph"/>; <see langword="null"/> otherwise. Distinct from the
        /// inherited, DataContract-serialized <see cref="NodeGraph.Class"/> (which stays unset for a
        /// type graph, unlike for a method or constructor graph) so this in-memory-only back-reference
        /// never changes legacy XML output; set by <see cref="Variable"/>. Used by
        /// <see cref="GraphKeys.For"/> to key a type graph as <c>&lt;variable id&gt;/type</c>.
        /// </summary>
        [IgnoreDataMember]
        public ClassGraph? OwningClass { get; internal set; }

        /// <summary>
        /// Creates a type graph and its <see cref="TypeReturnNode"/>.
        /// </summary>
        public TypeGraph()
        {
            _ = new TypeReturnNode(this);
        }
    }
}
