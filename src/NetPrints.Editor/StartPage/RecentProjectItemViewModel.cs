using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>One row of the recent list.</summary>
/// <param name="entry">The recent project it shows.</param>
/// <param name="owner">The tile that lists it, whose commands the row's buttons and menu run.</param>
/// <param name="groupTitle">The date group the row belongs to.</param>
/// <param name="showGroupHeader">Whether the row is the first of its group and shows the group's title above it.</param>
/// <param name="relativeDate">When the project was last opened, as a person would say it.</param>
internal sealed partial class RecentProjectItemViewModel(RecentProject entry, RecentProjectsTileViewModel owner, string groupTitle, bool showGroupHeader, string relativeDate) : ObservableObject
{
    /// <summary>The text of an unavailable row.</summary>
    public const string NotFound = "Not found";

    /// <summary>The longest path the row shows before it trims the middle.</summary>
    public const int MaxPathLength = 72;

    /// <summary>Gets the tile that lists the row.</summary>
    public RecentProjectsTileViewModel Owner => owner;

    /// <summary>Gets the project file path.</summary>
    public string Path => entry.Path;

    /// <summary>Gets the path as the row shows it: the middle replaced by an ellipsis when it is longer than <see cref="MaxPathLength"/>.</summary>
    public string DisplayPath => MiddleTrim(entry.Path);

    /// <summary>Gets the project name.</summary>
    public string DisplayName => entry.DisplayName;

    /// <summary>Gets a value indicating whether the entry is pinned.</summary>
    public bool Pinned => entry.Pinned;

    /// <summary>Gets or sets a value indicating whether the project file still exists; true until the tile's check says otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsAvailable { get; set; } = entry.IsAvailable;

    /// <summary>Gets the title of the date group the row belongs to.</summary>
    public string GroupTitle => groupTitle;

    /// <summary>Gets a value indicating whether the row is the first of its group.</summary>
    public bool ShowGroupHeader => showGroupHeader;

    /// <summary>Gets when the project was last opened, as a person would say it.</summary>
    public string RelativeDate => relativeDate;

    /// <summary>Gets "Not found" for a project whose file is gone, otherwise null.</summary>
    public string? StatusText => IsAvailable ? null : NotFound;

    /// <summary>Shortens a path by replacing its middle with an ellipsis, keeping the start and the file name.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The path itself when it is at most <see cref="MaxPathLength"/> long.</returns>
    public static string MiddleTrim(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length <= MaxPathLength)
        {
            return path;
        }

        int tail = MaxPathLength * 2 / 5;
        int head = MaxPathLength - tail - 1;
        return string.Concat(path.AsSpan(0, head), "…", path.AsSpan(path.Length - tail));
    }
}
