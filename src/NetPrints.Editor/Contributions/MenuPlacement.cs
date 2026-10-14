namespace NetPrints.Editor.Contributions;

/// <summary>Where a command sits in the menu bar.</summary>
/// <param name="Path">The top-level menu, such as <c>Build</c>.</param>
/// <param name="Group">The group within the menu; groups are separated.</param>
/// <param name="Order">The position within the group (or within the submenu).</param>
/// <param name="Submenu">The submenu of the group the command sits in, such as <c>Theme</c>, or null for an entry of the menu itself. The commands of one submenu in a group share one submenu entry, placed where its first command would be.</param>
public sealed record MenuPlacement(string Path, string Group, int Order, string? Submenu = null)
{
    /// <summary>Gets the place as a person reads it: the menu, and its submenu when there is one (<c>View › Theme</c>).</summary>
    public string Display => Submenu is null ? Path : Path + " › " + Submenu;
}
