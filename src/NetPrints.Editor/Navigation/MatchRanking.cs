namespace NetPrints.Editor.Navigation;

/// <summary>
/// The ranking of the command palette and go to anything (research R9): a prefix match first, then a match at the start of
/// a word, then any substring, then by name. Matching ignores case.
/// </summary>
public static class MatchRanking
{
    /// <summary>The name starts with the query (also the rank of every name for an empty query).</summary>
    public const int Prefix = 0;

    /// <summary>A word of the name starts with the query: after a space or punctuation, or at a lower-to-upper case change.</summary>
    public const int WordStart = 1;

    /// <summary>The query appears inside a word of the name.</summary>
    public const int Substring = 2;

    /// <summary>The name does not contain the query.</summary>
    public const int NoMatch = -1;

    /// <summary>Ranks a name against a query.</summary>
    /// <param name="name">The name.</param>
    /// <param name="query">The text typed, without surrounding spaces.</param>
    /// <returns><see cref="Prefix"/>, <see cref="WordStart"/>, <see cref="Substring"/> or <see cref="NoMatch"/>.</returns>
    public static int Rank(string name, string query)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length == 0 || name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return Prefix;
        }

        for (int i = 1; i < name.Length; i++)
        {
            if (IsWordBoundary(name[i - 1], name[i]) && name.AsSpan(i).StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return WordStart;
            }
        }

        return name.Contains(query, StringComparison.OrdinalIgnoreCase) ? Substring : NoMatch;
    }

    /// <summary>Gets the rank as a sort key that puts non-matches last.</summary>
    /// <param name="name">The name.</param>
    /// <param name="query">The text typed, without surrounding spaces.</param>
    /// <returns>The rank, or <see cref="int.MaxValue"/> for a name that does not match.</returns>
    public static int SortKey(string name, string query) => Rank(name, query) is var rank && rank == NoMatch ? int.MaxValue : rank;

    /// <summary>Keeps the items whose name matches the query and orders them by rank, then by name.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The candidates.</param>
    /// <param name="nameOf">Gets the text matched against the query.</param>
    /// <param name="query">The text typed, without surrounding spaces.</param>
    /// <returns>The matching items, best first.</returns>
    public static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, string> nameOf, string query)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nameOf);
        return items
            .Select(item => (Item: item, Name: nameOf(item), Rank: Rank(nameOf(item), query)))
            .Where(entry => entry.Rank != NoMatch)
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Item);
    }

    private static bool IsWordBoundary(char previous, char current) =>
        !char.IsLetterOrDigit(previous) || (char.IsLower(previous) && char.IsUpper(current));
}
