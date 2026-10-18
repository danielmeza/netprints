using System.Text;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>
/// One setting of a section: a label, the help text of the schema and the validate-then-save cycle of FR-042. A value that
/// the section's validator refuses shows its message in <see cref="Error"/> and is not saved; a valid one is saved at once.
/// </summary>
internal abstract partial class SettingsFieldViewModel : SettingsItemViewModel
{
    private readonly SettingsSection section;

    /// <summary>Creates the field of one key of a section.</summary>
    /// <param name="section">The section the key belongs to.</param>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="schema">The schema of the key.</param>
    protected SettingsFieldViewModel(SettingsSection section, string key, JsonObject schema)
        : base(LabelOf(key), schema["description"]?.GetValue<string>() ?? "")
    {
        this.section = section;
        Key = key;
        RequiresRestart = schema[SettingsSchema.RestartKeyword] is JsonValue marker && marker.TryGetValue(out bool restart) && restart;
    }

    /// <summary>Raised after a changed value was saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Gets the JSON name of the key inside its section.</summary>
    public string Key { get; }

    /// <summary>Gets the section id and the key joined by a dot, the name the settings table lists.</summary>
    public string QualifiedKey => $"{section.Id}.{Key}";

    /// <summary>Gets a value indicating whether a change takes effect only after a restart.</summary>
    public bool RequiresRestart { get; }

    /// <summary>Gets the message under the field, or <see langword="null"/> while the value is valid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; protected set; }

    /// <summary>Gets a value indicating whether <see cref="Error"/> has a message.</summary>
    public bool HasError => Error is not null;

    /// <summary>Reads the key as it is stored now.</summary>
    /// <returns>A detached copy of the stored value.</returns>
    protected JsonNode? Stored() => section.Read(Key);

    /// <summary>Builds the JSON value the field currently shows.</summary>
    /// <param name="node">The value on success.</param>
    /// <param name="problem">The message to show when the shown text is not a value of the key's type.</param>
    /// <returns><see langword="true"/> when <paramref name="node"/> was built.</returns>
    protected abstract bool TryBuildNode(out JsonNode? node, out string? problem);

    /// <summary>Validates what the field shows and, when it is valid and differs from the stored value, saves it.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the stored value now equals what the field shows.</returns>
    protected async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryBuildNode(out JsonNode? node, out string? problem))
        {
            Error = problem;
            return false;
        }

        if (JsonNode.DeepEquals(Stored(), node))
        {
            Error = null;
            return true;
        }

        IReadOnlyList<string> failures = await section.TrySetAsync(Key, node, cancellationToken);
        if (failures.Count > 0)
        {
            Error = string.Join(' ', failures);
            return false;
        }

        Error = null;
        Saved?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [RelayCommand]
    private Task CommitAsync(CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    private static string LabelOf(string key)
    {
        var words = new StringBuilder(key.Length + 4);
        foreach (char letter in key)
        {
            if (words.Length == 0)
            {
                words.Append(char.ToUpperInvariant(letter));
            }
            else if (char.IsUpper(letter))
            {
                words.Append(' ').Append(char.ToLowerInvariant(letter));
            }
            else
            {
                words.Append(letter);
            }
        }

        return words.ToString();
    }
}
