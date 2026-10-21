#nullable enable

namespace NetPrints.Graph
{
    /// <summary>
    /// A node that has other overloads the user can switch to. The editor asks for this role instead of testing
    /// node types.
    /// </summary>
    public interface IOverloadableNode
    {
        /// <summary>Gets the node's current overload.</summary>
        object CurrentOverload { get; }

        /// <summary>
        /// Switches the node to <paramref name="overload"/>.
        /// </summary>
        /// <param name="overload">An overload of the kind <see cref="CurrentOverload"/> returns.</param>
        /// <returns>A new node that replaces this one, or <see langword="null"/> when this node switched in place.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="overload"/> is not an overload of this node.</exception>
        Node? ChangeOverload(object overload);
    }
}
