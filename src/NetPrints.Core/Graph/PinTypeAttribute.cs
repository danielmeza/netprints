#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>Names the member of the node class that carries a data pin's type.</summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class PinTypeAttribute : Attribute
    {
        /// <summary>Creates the attribute.</summary>
        /// <param name="member">The name of an <see cref="ObservableValue{T}"/> member of <see cref="NetPrints.Core.BaseType"/> on the node class.</param>
        public PinTypeAttribute(string member)
        {
            Member = member;
        }

        /// <summary>Gets the member name.</summary>
        public string Member { get; }
    }
}
