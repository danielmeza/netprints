namespace NetPrints.Editor.NodeSearch;

/// <summary>Orders the entries of the providers before they are grouped into rows; the host's one ranking point.</summary>
internal interface ISuggestionRanker
{
    /// <summary>Orders the entries; the order of the categories is the order of the headers.</summary>
    /// <param name="entries">The entries of every provider, in provider order.</param>
    /// <returns>The same entries in display order.</returns>
    IEnumerable<NodeSearchEntry> Rank(IEnumerable<NodeSearchEntry> entries);
}

/// <summary>
/// The default ranker: the editor's categories in a fixed order, then the categories of extensions in order of first appearance.
/// Entries keep their order within a category.
/// </summary>
internal sealed class DefaultSuggestionRanker : ISuggestionRanker
{
    private static readonly string[] CategoryOrder =
    [
        SearchCategories.NetPrints,
        SearchCategories.PinVariables,
        SearchCategories.PinMethods,
        SearchCategories.ThisMethods,
        SearchCategories.ThisVariables,
        SearchCategories.StaticMethods,
        SearchCategories.StaticVariables,
        SearchCategories.MethodVariables,
        SearchCategories.Types,
        SearchCategories.PinStaticMethods,
        SearchCategories.GenericTypes,
        SearchCategories.GenericStaticMethods,
    ];

    private DefaultSuggestionRanker()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static DefaultSuggestionRanker Instance { get; } = new();

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Rank(IEnumerable<NodeSearchEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.OrderBy(entry => RankOf(entry.Category));
    }

    private static int RankOf(string category)
    {
        int index = Array.IndexOf(CategoryOrder, category);
        return index < 0 ? CategoryOrder.Length : index;
    }
}
