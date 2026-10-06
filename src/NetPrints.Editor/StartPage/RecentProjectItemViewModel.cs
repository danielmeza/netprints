using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>One row of the recent list.</summary>
/// <param name="entry">The recent project it shows.</param>
internal sealed class RecentProjectItemViewModel(RecentProject entry)
{
    /// <summary>Gets the project file path.</summary>
    public string Path => entry.Path;

    /// <summary>Gets the project name.</summary>
    public string DisplayName => entry.DisplayName;

    /// <summary>Gets a value indicating whether the entry is pinned.</summary>
    public bool Pinned => entry.Pinned;

    /// <summary>Gets a value indicating whether the project file still exists.</summary>
    public bool IsAvailable => entry.IsAvailable;

    /// <summary>Gets the text under the name: the path, with a note when the file is gone.</summary>
    public string Detail => entry.IsAvailable ? entry.Path : entry.Path + " (unavailable)";
}
