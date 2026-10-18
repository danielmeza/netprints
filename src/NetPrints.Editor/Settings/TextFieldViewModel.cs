using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Settings;

/// <summary>A <c>string</c> key: a text box that saves on Enter or when it loses focus.</summary>
internal partial class TextFieldViewModel : SettingsFieldViewModel
{
    /// <summary>Creates the text field of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    public TextFieldViewModel(SettingsSection section, string key, JsonObject schema)
        : base(section, key, schema)
    {
        Text = Stored() is JsonValue value && value.TryGetValue(out string? stored) ? stored : "";
    }

    /// <summary>Gets or sets the text of the value.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        node = JsonValue.Create(Text);
        problem = null;
        return true;
    }
}
