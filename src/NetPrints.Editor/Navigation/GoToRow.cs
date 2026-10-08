using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Navigation;

/// <summary>One row of go to anything: a group header or a result.</summary>
/// <param name="IsHeader">Whether the row only names a group.</param>
/// <param name="Title">The kind for a header, the result's title otherwise.</param>
/// <param name="Detail">The result's secondary text; empty for a header.</param>
/// <param name="Item">The result, or null for a header.</param>
public sealed record GoToRow(bool IsHeader, string Title, string Detail, GoToItem? Item);
