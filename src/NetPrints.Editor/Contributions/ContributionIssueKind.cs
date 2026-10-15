namespace NetPrints.Editor.Contributions;

/// <summary>Why the registry rejected or altered a contribution.</summary>
public enum ContributionIssueKind
{
    /// <summary>An id was already registered within the same kind; the first registration wins.</summary>
    DuplicateId,

    /// <summary>A gesture overlapped one of an earlier command in an overlapping scope; the first keeps it.</summary>
    GestureConflict,

    /// <summary>The descriptor failed validation and was ignored.</summary>
    InvalidDescriptor,
}
