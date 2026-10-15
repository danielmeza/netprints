using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>
/// The back and forward lists of the views the user navigated from (FR-063). Holds at most <see cref="Capacity"/> entries
/// each way; a new navigation clears forward; entries whose document is gone are skipped when going back or forward.
/// </summary>
public sealed class NavigationHistory
{
    /// <summary>The most entries kept each way.</summary>
    public const int Capacity = 50;

    private readonly List<NavigationEntry> back = [];
    private readonly List<NavigationEntry> forward = [];

    /// <summary>Gets the number of entries Back can go through, before skipping any whose document is gone.</summary>
    public int BackCount => back.Count;

    /// <summary>Gets the number of entries Forward can go through, before skipping any whose document is gone.</summary>
    public int ForwardCount => forward.Count;

    /// <summary>Records the view being left; clears forward. An entry identical to the last one is not recorded again.</summary>
    /// <param name="entry">The view being left.</param>
    public void Record(NavigationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        forward.Clear();
        if (back.Count > 0 && entry.SameView(back[^1]))
        {
            return;
        }

        Push(back, entry);
    }

    /// <summary>Gets whether Back would find an entry.</summary>
    /// <param name="exists">Tells whether a document still exists.</param>
    /// <returns><see langword="true"/> when an entry of the back list has an existing document.</returns>
    public bool CanGoBack(Func<DocumentId, bool> exists) => HasExisting(back, exists);

    /// <summary>Gets whether Forward would find an entry.</summary>
    /// <param name="exists">Tells whether a document still exists.</param>
    /// <returns><see langword="true"/> when an entry of the forward list has an existing document.</returns>
    public bool CanGoForward(Func<DocumentId, bool> exists) => HasExisting(forward, exists);

    /// <summary>Takes the latest existing back entry and puts <paramref name="current"/> on forward.</summary>
    /// <param name="current">The view being left, or null when it is not a graph.</param>
    /// <param name="exists">Tells whether a document still exists.</param>
    /// <returns>The entry to restore, or null when none has an existing document (then nothing changes except the skipped entries are dropped).</returns>
    public NavigationEntry? Back(NavigationEntry? current, Func<DocumentId, bool> exists) => Step(back, forward, current, exists);

    /// <summary>Takes the latest existing forward entry and puts <paramref name="current"/> on back.</summary>
    /// <param name="current">The view being left, or null when it is not a graph.</param>
    /// <param name="exists">Tells whether a document still exists.</param>
    /// <returns>The entry to restore, or null when none has an existing document.</returns>
    public NavigationEntry? Forward(NavigationEntry? current, Func<DocumentId, bool> exists) => Step(forward, back, current, exists);

    private static NavigationEntry? Step(List<NavigationEntry> from, List<NavigationEntry> to, NavigationEntry? current, Func<DocumentId, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        while (from.Count > 0)
        {
            NavigationEntry entry = from[^1];
            from.RemoveAt(from.Count - 1);
            if (exists(entry.Document))
            {
                if (current is not null)
                {
                    Push(to, current);
                }

                return entry;
            }
        }

        return null;
    }

    private static bool HasExisting(List<NavigationEntry> list, Func<DocumentId, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        return list.Exists(entry => exists(entry.Document));
    }

    private static void Push(List<NavigationEntry> list, NavigationEntry entry)
    {
        list.Add(entry);
        if (list.Count > Capacity)
        {
            list.RemoveAt(0);
        }
    }
}
