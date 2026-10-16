namespace NetPrints.Editor.NodeSearch;

/// <summary>Turns the entries of the providers into the rows of the list.</summary>
internal static class SuggestionRows
{
    /// <summary>
    /// Groups the entries by category in order of first appearance, drops repeated entries and puts a header row before each category
    /// (ported from the WPF <c>UpdateSuggestions</c>).
    /// </summary>
    /// <param name="entries">The entries of every provider, in provider order.</param>
    /// <returns>The header and suggestion rows in display order.</returns>
    public static List<SuggestionRowViewModel> Build(IEnumerable<NodeSearchEntry> entries)
    {
        var groups = new List<(string Category, List<SuggestionRowViewModel> Rows)>();
        var groupIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<NodeSearchEntry>();

        foreach (NodeSearchEntry entry in entries)
        {
            if (!seen.Add(entry))
            {
                continue;
            }

            if (!groupIndex.TryGetValue(entry.Category, out int index))
            {
                index = groups.Count;
                groupIndex[entry.Category] = index;
                groups.Add((entry.Category, []));
            }

            groups[index].Rows.Add(SuggestionRowViewModel.Create(entry));
        }

        var rows = new List<SuggestionRowViewModel>(seen.Count + groups.Count);
        foreach (var (category, categoryRows) in groups)
        {
            rows.Add(SuggestionRowViewModel.Header(category));
            rows.AddRange(categoryRows);
        }

        return rows;
    }
}
