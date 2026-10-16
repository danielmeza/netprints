#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>Marks a concrete node class whose pins the convention generator declares; the id is the stored kind id.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeKindAttribute : Attribute
    {
        /// <summary>Creates the marker.</summary>
        /// <param name="id">The stable kind id stored in graph documents.</param>
        public NodeKindAttribute(string id)
        {
            Id = id;
        }

        /// <summary>Gets the stable kind id.</summary>
        public string Id { get; }
    }
}
