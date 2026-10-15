using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The Learn card: the getting started guide, the keyboard shortcuts sheet, the documentation and the release notes.</summary>
internal sealed partial class LearnTileViewModel
{
    private readonly IProjectActions actions;
    private readonly IUrlLauncher launcher;

    /// <summary>Creates the tile.</summary>
    /// <param name="actions">Shows the keyboard shortcuts sheet.</param>
    /// <param name="launcher">Opens the links in the browser.</param>
    public LearnTileViewModel(IProjectActions actions, IUrlLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(launcher);
        this.actions = actions;
        this.launcher = launcher;
    }

    [RelayCommand]
    private void OpenGuide() => launcher.Open(LearnLinks.Guide);

    [RelayCommand]
    private Task ShowKeyboardShortcutsAsync(CancellationToken cancellationToken) => actions.ShowKeyboardShortcutsAsync(cancellationToken);

    [RelayCommand]
    private void OpenDocumentation() => launcher.Open(LearnLinks.Documentation);

    [RelayCommand]
    private void OpenReleaseNotes() => launcher.Open(LearnLinks.ReleaseNotes);
}
