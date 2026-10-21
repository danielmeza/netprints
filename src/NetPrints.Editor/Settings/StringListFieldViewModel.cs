using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Settings;
using PathKinds = NetPrints.Extensibility.Settings.PathKind;

namespace NetPrints.Editor.Settings;

/// <summary>An <c>array</c> of strings: a list to add to, remove from and reorder; each change is saved at once.</summary>
internal sealed partial class StringListFieldViewModel : SettingsFieldViewModel
{
    private readonly IFilePickerService? picker;

    /// <summary>Creates the list field of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    /// <param name="pathKind">What the entries name when they are paths; <see langword="null"/> for plain strings.</param>
    /// <param name="picker">The picker Browse opens, or <see langword="null"/> when the host has none.</param>
    public StringListFieldViewModel(SettingsSection section, string key, JsonObject schema, PathKind? pathKind, IFilePickerService? picker)
        : base(section, key, schema)
    {
        PathKind = pathKind;
        this.picker = picker;
        Load();
    }

    /// <summary>Gets what the entries name when they are paths; <see langword="null"/> for plain strings.</summary>
    public PathKind? PathKind { get; }

    /// <summary>Gets a value indicating whether the entries are paths a picker can fill.</summary>
    public bool CanBrowse => PathKind is not null && picker is not null;

    /// <summary>Gets the entries in order.</summary>
    public ObservableCollection<StringListItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets the text of the entry to add.</summary>
    [ObservableProperty]
    public partial string NewItem { get; set; } = "";

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        node = new JsonArray([.. Items.Select(item => JsonValue.Create(item.Text))]);
        problem = null;
        return true;
    }

    [RelayCommand]
    private async Task AddAsync(CancellationToken cancellationToken)
    {
        string text = NewItem.Trim();
        if (text.Length == 0)
        {
            return;
        }

        Items.Add(new StringListItemViewModel(text));
        NewItem = "";
        await SaveOrRevertAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RemoveAsync(StringListItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is not null && Items.Remove(item))
        {
            await SaveOrRevertAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private Task MoveUpAsync(StringListItemViewModel? item, CancellationToken cancellationToken) => MoveAsync(item, -1, cancellationToken);

    [RelayCommand]
    private Task MoveDownAsync(StringListItemViewModel? item, CancellationToken cancellationToken) => MoveAsync(item, 1, cancellationToken);

    [RelayCommand]
    private async Task BrowseAsync(CancellationToken cancellationToken)
    {
        if (picker is null || PathKind is not { } kind)
        {
            return;
        }

        string? picked = kind == PathKinds.Folder
            ? await picker.OpenFolderAsync(Strings.Settings_ChooseFolder)
            : await picker.OpenFileAsync(Strings.Settings_ChooseFile, [new FileFilter(Strings.Settings_AllFiles, ["*"])]);
        if (picked is not null)
        {
            Items.Add(new StringListItemViewModel(picked));
            await SaveOrRevertAsync(cancellationToken);
        }
    }

    private async Task MoveAsync(StringListItemViewModel? item, int offset, CancellationToken cancellationToken)
    {
        int index = item is null ? -1 : Items.IndexOf(item);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= Items.Count)
        {
            return;
        }

        Items.Move(index, target);
        await SaveOrRevertAsync(cancellationToken);
    }

    private async Task SaveOrRevertAsync(CancellationToken cancellationToken)
    {
        if (!await SaveAsync(cancellationToken))
        {
            Load();
        }
    }

    private void Load()
    {
        Items.Clear();
        if (Stored() is JsonArray stored)
        {
            foreach (JsonNode? entry in stored)
            {
                if (entry is JsonValue value && value.TryGetValue(out string? text))
                {
                    Items.Add(new StringListItemViewModel(text));
                }
            }
        }
    }
}

/// <summary>One entry of a <see cref="StringListFieldViewModel"/>.</summary>
/// <param name="Text">The entry's text.</param>
internal sealed record StringListItemViewModel(string Text);
