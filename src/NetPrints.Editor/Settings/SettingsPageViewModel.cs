using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Settings;

/// <summary>A page of the Settings document: a title and the entries under it.</summary>
/// <param name="id">The id of the page.</param>
/// <param name="title">The title in the page list.</param>
/// <param name="items">The entries, top to bottom.</param>
/// <param name="isChild">Whether the page is listed under another page (an extension page under Extensions or under the page it names as its parent).</param>
internal sealed partial class SettingsPageViewModel(string id, string title, IReadOnlyList<SettingsItemViewModel> items, bool isChild) : ObservableObject
{
    /// <summary>Gets the id of the page.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the title in the page list.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the entries, top to bottom.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items { get; } = items;

    /// <summary>Gets a value indicating whether the page is listed under another page (an extension page under Extensions or under the page it names as its parent).</summary>
    public bool IsChild { get; } = isChild;

    /// <summary>Gets a value indicating whether the page has an entry that passes the search.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; } = true;

    /// <summary>Applies a search to the entries.</summary>
    /// <param name="query">The search text; empty shows every entry.</param>
    public void ApplyFilter(string query)
    {
        foreach (SettingsItemViewModel item in Items)
        {
            item.IsVisible = item.Matches(query);
        }

        IsVisible = query.Length == 0 || Items.Any(item => item.IsVisible);
    }

    /// <summary>Shows the page because a page under it matches the search.</summary>
    public void ShowForChild() => IsVisible = true;
}
