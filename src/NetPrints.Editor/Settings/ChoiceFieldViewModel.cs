using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Settings;

/// <summary>A key with a closed set of values (<c>enum</c>, or the provider named by <c>x-netprints-choices</c>): a drop-down that saves when a value is chosen.</summary>
internal sealed partial class ChoiceFieldViewModel : SettingsFieldViewModel
{
    /// <summary>Creates the drop-down of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    /// <param name="choices">The values offered.</param>
    public ChoiceFieldViewModel(SettingsSection section, string key, JsonObject schema, IReadOnlyList<string> choices)
        : base(section, key, schema)
    {
        Choices = choices;
        Selected = ReadStored();
    }

    /// <summary>Gets the values the drop-down offers.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>Gets the chosen value.</summary>
    [ObservableProperty]
    public partial string? Selected { get; private set; }

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        node = JsonValue.Create(Selected);
        problem = null;
        return true;
    }

    [RelayCommand]
    private async Task ChooseAsync(string? choice, CancellationToken cancellationToken)
    {
        if (choice is null || choice == Selected)
        {
            return;
        }

        Selected = choice;
        if (!await SaveAsync(cancellationToken))
        {
            Selected = ReadStored();
        }
    }

    private string? ReadStored() => Stored() is JsonValue value && value.TryGetValue(out string? stored) ? stored : null;
}
