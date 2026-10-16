using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;

namespace NetPrints.Editor.NodeSearch;

/// <summary>
/// A row of the node search list: either a category header or a suggestion (PAR-52).
/// Ports the WPF <c>SuggestionListConverter</c> text and icon logic.
/// </summary>
public sealed class SuggestionRowViewModel
{
    private SuggestionRowViewModel(string category, NodeSearchEntry? entry, string text, string iconKey, bool isHeader)
    {
        Category = category;
        Entry = entry;
        Text = text;
        IconKey = iconKey;
        IsHeader = isHeader;
        SearchText = isHeader || entry is null ? category : $"{category} {text} {string.Join(' ', entry.Keywords)}".TrimEnd();
    }

    /// <summary>The category this row belongs to (also this row's own text, for a header row).</summary>
    public string Category { get; }

    /// <summary>The suggestion the row creates a node from; null for headers.</summary>
    internal NodeSearchEntry? Entry { get; }

    /// <summary>Display text for the row.</summary>
    public string Text { get; }

    /// <summary>File name of the 16-px icon in the editor assets.</summary>
    public string IconKey { get; }

    /// <summary>Whether this row is a category header rather than a suggestion.</summary>
    public bool IsHeader { get; }

    /// <summary>Text matched by the multi-term, case-insensitive search.</summary>
    public string SearchText { get; }

    /// <summary>Creates a category header row.</summary>
    /// <param name="category">Category name, which groups the rows.</param>
    /// <param name="registry">The registry whose strings translate the header; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>A header row for the category, whose text is the category's translation.</returns>
    internal static SuggestionRowViewModel Header(string category, ExtensionRegistry? registry = null) =>
        new(category, null, NodeTexts.Category(category, registry), "", isHeader: true);

    /// <summary>Creates a suggestion row from an entry.</summary>
    /// <param name="entry">The suggestion; its category, text and icon become the row's.</param>
    /// <param name="registry">The registry whose strings translate the name of a node kind's suggestion; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>A suggestion row for the entry.</returns>
    internal static SuggestionRowViewModel Create(NodeSearchEntry entry, ExtensionRegistry? registry = null) =>
        new(entry.Category, entry, entry is NodeKindEntry ? NodeTexts.Suggestion(entry.Text, registry) : entry.Text, entry.IconKey, isHeader: false);

    /// <summary>Whether every search term occurs in <see cref="SearchText"/> (case-insensitive).</summary>
    public bool Matches(IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (SearchText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns <see cref="Text"/>.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;

    /// <summary>Text shown for a method: declaring type, name (operators by display name), parameters and return types.</summary>
    public static string FormatMethod(MethodSpecifier methodSpecifier)
    {
        string name = OperatorUtil.TryGetOperatorInfo(methodSpecifier, out var operatorInfo)
            ? $"Operator {operatorInfo.DisplayName}"
            : methodSpecifier.Name;

        string parameters = string.Join(", ", methodSpecifier.Parameters);
        string text = $"{methodSpecifier.DeclaringType} {name} ({parameters})";

        if (methodSpecifier.ReturnTypes.Count > 0)
        {
            text += $" : {string.Join(", ", methodSpecifier.ReturnTypes)}";
        }

        return text;
    }
}
