namespace NetPrints.Cli.Git;

/// <summary>The reason a three-way merge of two graphs cannot be completed by identity (contracts/git.md §2).</summary>
internal enum MergeConflictKind
{
    /// <summary>A scalar field of the class or of a member was changed to different values on both sides.</summary>
    Scalar,

    /// <summary>A member, node, local or accessor was deleted on one side and changed on the other.</summary>
    DeleteModify,

    /// <summary>A node's properties were changed differently on both sides, or the same node id was added with different content.</summary>
    NodeProperty,

    /// <summary>A pin's name or unconnected value was changed differently on both sides.</summary>
    PinValue,

    /// <summary>The merged graph connects more than one source into one data input pin.</summary>
    DataInputTwice,

    /// <summary>The merged graph holds a connection to a node that no longer exists.</summary>
    DanglingConnection,

    /// <summary>The merged class holds two members with the same id, or the same name within one kind of member.</summary>
    DuplicateMember,
}

/// <summary>One conflict of a merge: what kind it is and where in the document it was found.</summary>
/// <param name="Kind">The kind of conflict.</param>
/// <param name="Path">Where it was found, for example <c>methods[m1].nodes[n3].pins[in.data.value]</c>.</param>
internal sealed record MergeConflict(MergeConflictKind Kind, string Path);
