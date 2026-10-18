using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Settings;

/// <summary>An <c>integer</c> or <c>number</c> key: a text box that checks the text is a number, then lets the section's validator check the range.</summary>
internal sealed partial class NumberFieldViewModel : SettingsFieldViewModel
{
    private const string IntegerType = "integer";
    private const string MinimumKeyword = "minimum";
    private const string MaximumKeyword = "maximum";

    private readonly bool whole;

    /// <summary>Creates the number field of one key.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    public NumberFieldViewModel(SettingsSection section, string key, JsonObject schema)
        : base(section, key, schema)
    {
        whole = SettingsFieldFactory.TypeOf(schema) == IntegerType;
        Minimum = Bound(schema, MinimumKeyword);
        Maximum = Bound(schema, MaximumKeyword);
        Text = Stored()?.ToJsonString() ?? "";
    }

    /// <summary>Gets the smallest value the schema allows, or <see langword="null"/> for no lower bound.</summary>
    public double? Minimum { get; }

    /// <summary>Gets the largest value the schema allows, or <see langword="null"/> for no upper bound.</summary>
    public double? Maximum { get; }

    /// <summary>Gets or sets the text of the number.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <inheritdoc/>
    protected override bool TryBuildNode(out JsonNode? node, out string? problem)
    {
        string text = Text.Trim();
        node = null;
        problem = null;
        if (whole)
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
            {
                node = JsonValue.Create(integer);
                return true;
            }

            problem = Strings.Settings_Error_WholeNumber;
            return false;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            node = JsonValue.Create(number);
            return true;
        }

        problem = Strings.Settings_Error_Number;
        return false;
    }

    private static double? Bound(JsonObject schema, string keyword) =>
        schema[keyword] is JsonValue value && double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double bound) ? bound : null;
}
