using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Settings;

/// <summary>A <c>boolean</c> key: a toggle that saves when it flips.</summary>
internal sealed partial class ToggleFieldViewModel : SettingsFieldViewModel
{
    /// <summary>Creates the toggle of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    public ToggleFieldViewModel(SettingsSection section, string key, JsonObject schema)
        : base(section, key, schema)
    {
        IsOn = ReadStored();
    }

    /// <summary>Gets a value indicating whether the setting is on.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; private set; }

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        node = JsonValue.Create(IsOn);
        problem = null;
        return true;
    }

    [RelayCommand]
    private async Task ToggleAsync(CancellationToken cancellationToken)
    {
        IsOn = !IsOn;
        if (!await SaveAsync(cancellationToken))
        {
            IsOn = ReadStored();
        }
    }

    private bool ReadStored() => Stored() is JsonValue value && value.TryGetValue(out bool on) && on;
}
