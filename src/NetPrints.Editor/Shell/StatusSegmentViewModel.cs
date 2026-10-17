using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Shell;

/// <summary>A text segment of the status bar that follows the shell: it reads its text from the shell each time it is refreshed.</summary>
/// <param name="shell">The shell the text is read from.</param>
/// <param name="read">Reads the text from the shell; <see langword="null"/> or empty when there is nothing to show.</param>
public partial class StatusSegmentViewModel(ShellViewModel shell, Func<ShellViewModel, string?> read) : ObservableObject, IStatusSegment
{
    /// <summary>Gets the text last read from the shell.</summary>
    [ObservableProperty]
    public partial string? Text { get; private set; }

    /// <summary>Reads the text from the shell again.</summary>
    public void Refresh() => Text = read(shell);
}

/// <summary>The build state segment: a status segment of its own type so its view carries the build state's automation id.</summary>
/// <param name="shell">The shell the text is read from.</param>
/// <param name="read">Reads the text from the shell.</param>
public sealed class BuildStatusSegmentViewModel(ShellViewModel shell, Func<ShellViewModel, string?> read) : StatusSegmentViewModel(shell, read);
