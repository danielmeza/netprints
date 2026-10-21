#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>Marks an abstract node base class whose pins the convention generator declares; derived classes continue after them.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeBaseAttribute : Attribute
    {
    }
}
