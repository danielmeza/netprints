using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.Settings;

/// <summary>One value of a drop-down and the text it shows.</summary>
/// <param name="Value">The stored value.</param>
/// <param name="Label">The text the drop-down shows for the value.</param>
internal sealed record ChoiceOption(string Value, string Label);

/// <summary>A key with a closed set of values (<c>enum</c>, or the provider named by <c>x-netprints-choices</c>): a drop-down that saves when a value is chosen.</summary>
internal sealed partial class ChoiceFieldViewModel : SettingsFieldViewModel
{
    /// <summary>Creates the drop-down of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    /// <param name="choices">The values offered.</param>
    /// <param name="labelOf">Gives the text of a value; the value itself when <see langword="null"/>.</param>
    public ChoiceFieldViewModel(SettingsSection section, string key, JsonObject schema, IReadOnlyList<string> choices, Func<string, string>? labelOf = null)
        : base(section, key, schema)
    {
        Choices = choices;
        Options = [.. choices.Select(value => new ChoiceOption(value, labelOf?.Invoke(value) ?? value))];
        Selected = ReadStored();
    }

    /// <summary>Gets the values the drop-down offers.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>Gets the values with their texts, in the order of <see cref="Choices"/>.</summary>
    public IReadOnlyList<ChoiceOption> Options { get; }

    /// <summary>Gets the chosen value.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedOption))]
    public partial string? Selected { get; private set; }

    /// <summary>Gets the option of the chosen value, or <see langword="null"/> when the stored value is not offered.</summary>
    public ChoiceOption? SelectedOption => Options.FirstOrDefault(option => option.Value == Selected);

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        node = JsonValue.Create(Selected);
        problem = null;
        return true;
    }

    [RelayCommand]
    private Task ChooseOptionAsync(ChoiceOption? option, CancellationToken cancellationToken) => ChooseAsync(option?.Value, cancellationToken);

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
