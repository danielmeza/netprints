#nullable enable
namespace NetPrints.Graph
{
    /// <summary>What a pin means to its node, set by the pin convention generator or by the node itself.</summary>
    public enum PinRole
    {
        /// <summary>A plain input or output; the default.</summary>
        Argument,

        /// <summary>The object a call or access acts on.</summary>
        Target,

        /// <summary>The value a node produces.</summary>
        Result,

        /// <summary>An <c>out</c> or <c>ref</c> value carried after a call.</summary>
        OutParam,

        /// <summary>The exception a node raises; it never makes a node pure.</summary>
        Exception,
    }
}
