#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>
    /// A node that names a class member by its signature, so renaming the member retargets the node.
    /// Implementing this is enough for <see cref="Core.MemberRename"/> to find the node; nothing is registered.
    /// </summary>
    public interface IMemberReferencingNode
    {
        /// <summary>
        /// Whether this node points at <paramref name="member"/>.
        /// </summary>
        /// <param name="member">The member's identity.</param>
        /// <returns><see langword="true"/> if the node refers to the member.</returns>
        bool RefersTo(MemberKey member);

        /// <summary>
        /// Points the node at the renamed member, keeping its pins and connections.
        /// </summary>
        /// <param name="member">The member the node refers to now (<see cref="RefersTo"/> is true for it).</param>
        /// <param name="newName">The member's new name.</param>
        /// <returns>The action that restores the node's previous target.</returns>
        Action Retarget(MemberKey member, string newName);
    }
}
