using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.StartPage;

/// <summary>The what's new tile: the release notes bundled with the editor.</summary>
internal sealed partial class WhatsNewTileViewModel
{
    private readonly IUrlLauncher launcher;

    /// <summary>Creates the tile.</summary>
    /// <param name="markdown">The release notes.</param>
    /// <param name="launcher">Opens the links.</param>
    public WhatsNewTileViewModel(string markdown, IUrlLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        this.launcher = launcher;
        Blocks = WhatsNewRenderer.Parse(markdown);
    }

    /// <summary>Gets the release notes' blocks.</summary>
    public IReadOnlyList<WhatsNewBlock> Blocks { get; }

    [RelayCommand]
    private void OpenLink(string url) => launcher.Open(url);
}
