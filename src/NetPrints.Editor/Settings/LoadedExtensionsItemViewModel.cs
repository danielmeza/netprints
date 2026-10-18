namespace NetPrints.Editor.Settings;

/// <summary>One row of the loaded extensions list.</summary>
/// <param name="Id">The extension id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version text of the manifest.</param>
/// <param name="Source">The folder the extension was loaded from, or the built-in marker.</param>
internal sealed record LoadedExtensionRow(string Id, string Name, string Version, string Source);

/// <summary>The block of the Extensions page that lists the loaded extensions with their version and source.</summary>
internal sealed class LoadedExtensionsItemViewModel(IReadOnlyList<LoadedExtensionRow> rows)
    : SettingsItemViewModel(Strings.Settings_LoadedExtensions_Label, Strings.Settings_LoadedExtensions_Description)
{
    /// <summary>Gets the loaded extensions, ordered by name.</summary>
    public IReadOnlyList<LoadedExtensionRow> Rows { get; } = rows;
}
