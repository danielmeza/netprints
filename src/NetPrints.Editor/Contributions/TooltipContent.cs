namespace NetPrints.Editor.Contributions;

/// <summary>The text of a tooltip.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Lines">Detail lines under the heading.</param>
/// <param name="Documentation">Documentation text, or null.</param>
public sealed record TooltipContent(string Title, IReadOnlyList<string> Lines, string? Documentation = null);
