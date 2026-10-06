namespace NetPrints.Editor.State;

/// <summary>
/// The recently opened projects (FR-041, state-files.md §3): pinned entries first, then the most recent; at most
/// <see cref="MaxUnpinned"/> unpinned entries, the oldest dropped; pinned entries never dropped. Every change is saved at once.
/// Removing an entry never touches the project's files. Not thread safe.
/// </summary>
public sealed class RecentProjects
{
    /// <summary>The most unpinned entries kept.</summary>
    public const int MaxUnpinned = 20;

    private readonly IEditorStateStore store;
    private readonly IEditorFileSystem fileSystem;
    private readonly TimeProvider time;
    private readonly StringComparison comparison;
    private readonly List<RecentEntry> entries;

    /// <summary>Creates the list over the saved one.</summary>
    /// <param name="store">Where the list is read from and saved to.</param>
    /// <param name="fileSystem">Tells whether a project file still exists.</param>
    /// <param name="time">The clock stamped on opened projects.</param>
    /// <param name="pathComparison">How paths are compared; null for case-insensitive on Windows only.</param>
    public RecentProjects(IEditorStateStore store, IEditorFileSystem fileSystem, TimeProvider time, StringComparison? pathComparison = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        comparison = pathComparison ?? (OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        entries = [.. store.LoadRecent().Entries.OrderByDescending(entry => entry.LastOpenedUtc)];
    }

    /// <summary>Lists the projects, pinned first and then the most recent.</summary>
    /// <param name="search">Keeps the entries whose name or path contains this text, ignoring case; null or empty keeps all.</param>
    /// <returns>The entries, each flagged with whether its file still exists.</returns>
    public IReadOnlyList<RecentProject> List(string? search = null) =>
        [.. entries.Where(entry => string.IsNullOrEmpty(search)
                || entry.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Pinned)
            .Select(entry => new RecentProject(entry.Path, entry.DisplayName, entry.LastOpenedUtc, entry.Pinned, fileSystem.FileExists(entry.Path)))];

    /// <summary>Records that a project was opened or created: it moves to the front, keeping its pin.</summary>
    /// <param name="path">The project file's path.</param>
    /// <param name="displayName">The project's name.</param>
    public void Record(string path, string displayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        int index = IndexOf(path);
        bool pinned = index >= 0 && entries[index].Pinned;
        if (index >= 0)
        {
            entries.RemoveAt(index);
        }

        entries.Insert(0, new RecentEntry(path, displayName, time.GetUtcNow().UtcDateTime, pinned));
        Commit();
    }

    /// <summary>Pins an entry so it stays and comes first.</summary>
    /// <param name="path">The project file's path.</param>
    public void Pin(string path) => SetPinned(path, true);

    /// <summary>Unpins an entry.</summary>
    /// <param name="path">The project file's path.</param>
    public void Unpin(string path) => SetPinned(path, false);

    /// <summary>Forgets an entry; the project's files are not touched.</summary>
    /// <param name="path">The project file's path.</param>
    public void Remove(string path)
    {
        int index = IndexOf(path);
        if (index >= 0)
        {
            entries.RemoveAt(index);
            Commit();
        }
    }

    private void SetPinned(string path, bool pinned)
    {
        int index = IndexOf(path);
        if (index >= 0 && entries[index].Pinned != pinned)
        {
            entries[index] = entries[index] with { Pinned = pinned };
            Commit();
        }
    }

    private int IndexOf(string path) => entries.FindIndex(entry => string.Equals(entry.Path, path, comparison));

    private void Commit()
    {
        int unpinned = 0;
        entries.RemoveAll(entry => !entry.Pinned && ++unpinned > MaxUnpinned);
        store.SaveRecent(new RecentState(StateFile.CurrentVersion, [.. entries]));
    }
}
