namespace NetPrints.Editor.Contributions;

/// <summary>One problem the registry found in a contribution.</summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="Id">The id of the offending contribution (possibly malformed).</param>
/// <param name="Owner">The contributor: <see cref="ContributionIds.Owner"/> for built-ins.</param>
/// <param name="Message">A human-readable description.</param>
public sealed record ContributionIssue(ContributionIssueKind Kind, string Id, string Owner, string Message);
