#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>
    /// Records the pins a class declares, in order, as <c>&lt;direction&gt;.&lt;exec|data|type&gt;.&lt;key&gt;</c>. The convention
    /// generator emits it so a derived class in another assembly knows where its own pins start.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodePinLayoutAttribute : Attribute
    {
        /// <summary>Creates the attribute.</summary>
        /// <param name="pins">The declared pins, in declaration order.</param>
        public NodePinLayoutAttribute(params string[] pins)
        {
            Pins = pins;
        }

        /// <summary>Gets the declared pins, in declaration order.</summary>
        public string[] Pins { get; }
    }
}
