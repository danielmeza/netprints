using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>One row of the recent list.</summary>
/// <param name="entry">The recent project it shows.</param>
/// <param name="groupTitle">The date group the row belongs to.</param>
/// <param name="showGroupHeader">Whether the row is the first of its group and shows the group's title above it.</param>
/// <param name="relativeDate">When the project was last opened, as a person would say it.</param>
internal sealed class RecentProjectItemViewModel(RecentProject entry, string groupTitle, bool showGroupHeader, string relativeDate)
{
    /// <summary>The text of an unavailable row.</summary>
    public const string NotFound = "Not found";

    /// <summary>Gets the project file path.</summary>
    public string Path => entry.Path;

    /// <summary>Gets the project name.</summary>
    public string DisplayName => entry.DisplayName;

    /// <summary>Gets a value indicating whether the entry is pinned.</summary>
    public bool Pinned => entry.Pinned;

    /// <summary>Gets a value indicating whether the project file still exists.</summary>
    public bool IsAvailable => entry.IsAvailable;

    /// <summary>Gets the title of the date group the row belongs to.</summary>
    public string GroupTitle => groupTitle;

    /// <summary>Gets a value indicating whether the row is the first of its group.</summary>
    public bool ShowGroupHeader => showGroupHeader;

    /// <summary>Gets when the project was last opened, as a person would say it.</summary>
    public string RelativeDate => relativeDate;

    /// <summary>Gets "Not found" for a project whose file is gone, otherwise null.</summary>
    public string? StatusText => entry.IsAvailable ? null : NotFound;
}
