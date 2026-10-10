namespace NetPrints.Editor.Contributions;

/// <summary>Where a command sits in the menu bar.</summary>
/// <param name="Path">The top-level menu, such as <c>Build</c>.</param>
/// <param name="Group">The group within the menu; groups are separated.</param>
/// <param name="Order">The position within the group.</param>
public sealed record MenuPlacement(string Path, string Group, int Order);
