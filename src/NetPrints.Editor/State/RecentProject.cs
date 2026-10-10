namespace NetPrints.Editor.State;

/// <summary>A recent project as the start page shows it.</summary>
/// <param name="Path">The project file's path as it was opened.</param>
/// <param name="DisplayName">The project's name.</param>
/// <param name="LastOpenedUtc">When it was last opened or created.</param>
/// <param name="Pinned">Whether the user pinned it.</param>
/// <param name="IsAvailable">Whether the project file still exists.</param>
public sealed record RecentProject(string Path, string DisplayName, DateTime LastOpenedUtc, bool Pinned, bool IsAvailable);
