using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Settings;

/// <summary>One row of the loaded extensions list.</summary>
/// <param name="Id">The extension id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version text of the manifest.</param>
/// <param name="Source">The folder the extension was loaded from, or the built-in marker.</param>
internal sealed record LoadedExtensionRow(string Id, string Name, string Version, string Source);

/// <summary>The block of the Extensions page that lists the loaded extensions with their version and source; the Settings document replaces the rows when the extension host loads another set.</summary>
internal sealed partial class LoadedExtensionsItemViewModel(IReadOnlyList<LoadedExtensionRow> rows)
    : SettingsItemViewModel(Strings.Settings_LoadedExtensions_Label, Strings.Settings_LoadedExtensions_Description)
{
    /// <summary>Gets or sets the loaded extensions, ordered by name.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LoadedExtensionRow> Rows { get; set; } = rows;
}
