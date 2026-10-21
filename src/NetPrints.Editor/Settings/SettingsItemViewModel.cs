using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Settings;

/// <summary>One entry of a settings page: a field, or a block that is not a field. Search filters the entries by label and description.</summary>
internal abstract partial class SettingsItemViewModel(string label, string description) : ObservableObject
{
    /// <summary>Gets the text shown beside the entry.</summary>
    public string Label { get; } = label;

    /// <summary>Gets the help text under the label; empty when there is none.</summary>
    public string Description { get; } = description;

    /// <summary>Gets a value indicating whether the entry passes the current search.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Tells whether the label or the description contains a search text, ignoring case in the current culture.</summary>
    /// <param name="query">The search text; empty matches everything.</param>
    /// <returns><see langword="true"/> when the entry matches.</returns>
    public bool Matches(string query) =>
        query.Length == 0
        || Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}
