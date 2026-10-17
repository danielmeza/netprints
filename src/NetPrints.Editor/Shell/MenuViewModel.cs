using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Shell;

/// <summary>A top-level menu: its header and its entries, with dividers between groups.</summary>
/// <param name="Header">The menu name.</param>
/// <param name="Items">The entries in order.</param>
public sealed record MenuViewModel(string Header, IReadOnlyList<CommandEntryViewModel> Items)
{
    /// <summary>Gets the header as shown: the translation of <c>Menu.&lt;header&gt;</c> for the UI culture, or the header itself.</summary>
    public string Title => new MenuPlacement(Header, "", 0).Label.Resolve(Strings.ResourceManager, null);

    /// <summary>Gets the automation id: <c>Menu.&lt;header&gt;</c>.</summary>
    public string AutomationId => AutomationIds.MenuPrefix + Header;

    /// <summary>Gets a value indicating whether the menu has any command.</summary>
    public bool HasItems => Items.Count > 0;
}
