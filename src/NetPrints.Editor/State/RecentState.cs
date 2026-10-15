namespace NetPrints.Editor.State;

/// <summary>The content of <c>recent.json</c>.</summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="Entries">The remembered projects, in no particular order.</param>
public sealed record RecentState(int SchemaVersion, IReadOnlyList<RecentEntry> Entries) : IStateFile
{
    /// <summary>Gets a state with no entries.</summary>
    public static RecentState Empty { get; } = new(StateFile.CurrentVersion, []);
}

/// <summary>One remembered project.</summary>
/// <param name="Path">The project file's path as it was opened.</param>
/// <param name="DisplayName">The project's name.</param>
/// <param name="LastOpenedUtc">When it was last opened or created.</param>
/// <param name="Pinned">Whether the user pinned it.</param>
public sealed record RecentEntry(string Path, string DisplayName, DateTime LastOpenedUtc, bool Pinned);
