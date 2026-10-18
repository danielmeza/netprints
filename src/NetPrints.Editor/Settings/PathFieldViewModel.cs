using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>A <c>string</c> key marked with <c>x-netprints-path</c>: a text box with a Browse button that opens the file or folder picker.</summary>
internal sealed partial class PathFieldViewModel : TextFieldViewModel
{
    private readonly IFilePickerService? picker;

    /// <summary>Creates the path field of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    /// <param name="kind">Whether the path names a file or a folder.</param>
    /// <param name="picker">The picker Browse opens, or <see langword="null"/> when the host has none.</param>
    public PathFieldViewModel(SettingsSection section, string key, JsonObject schema, PathKind kind, IFilePickerService? picker)
        : base(section, key, schema)
    {
        Kind = kind;
        this.picker = picker;
    }

    /// <summary>Gets what the path names.</summary>
    public PathKind Kind { get; }

    [RelayCommand]
    private async Task BrowseAsync(CancellationToken cancellationToken)
    {
        if (picker is null)
        {
            return;
        }

        string? picked = Kind == PathKind.Folder
            ? await picker.OpenFolderAsync(Strings.Settings_ChooseFolder)
            : await picker.OpenFileAsync(Strings.Settings_ChooseFile, [new FileFilter(Strings.Settings_AllFiles, ["*"])]);
        if (picked is null)
        {
            return;
        }

        Text = picked;
        await SaveAsync(cancellationToken);
    }
}
