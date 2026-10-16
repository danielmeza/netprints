using NetPrints.Core;

namespace NetPrints.Editor.Contributions;

/// <summary>Where a command sits in the menu bar.</summary>
/// <param name="Path">The id of the top-level menu, such as <c>Build</c>; its header comes from <see cref="Label"/>.</param>
/// <param name="Group">The group within the menu; groups are separated.</param>
/// <param name="Order">The position within the group (or within the submenu).</param>
/// <param name="Submenu">The submenu of the group the command sits in, such as <c>Theme</c>, or null for an entry of the menu itself. The commands of one submenu in a group share one submenu entry, placed where its first command would be.</param>
public sealed record MenuPlacement(string Path, string Group, int Order, string? Submenu = null)
{
    /// <summary>Gets the menu header as a localizable text: the key <c>Menu.&lt;id&gt;</c>, with the id as the default text.</summary>
    public LocalizedText Label => new(ConventionKeys.MenuKey(Path), Path);

    /// <summary>Gets the place as a person reads it: the menu, and its submenu when there is one (<c>View › Theme</c>).</summary>
    public string Display => Submenu is null ? Path : Path + " › " + Submenu;
}
