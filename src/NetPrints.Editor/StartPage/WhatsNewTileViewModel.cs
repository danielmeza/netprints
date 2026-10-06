using CommunityToolkit.Mvvm.Input;

namespace NetPrints.Editor.StartPage;

/// <summary>The what's new tile: the bundled release notes and the link to the full ones.</summary>
internal sealed partial class WhatsNewTileViewModel
{
    /// <summary>The address of the releases page on GitHub.</summary>
    public const string ReleasesUrl = "https://github.com/danielmeza/netprints/releases";

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

    /// <summary>Gets the address of the releases page, for the tile's link.</summary>
    public string ReleasesAddress => ReleasesUrl;

    /// <summary>Gets the release notes' blocks.</summary>
    public IReadOnlyList<WhatsNewBlock> Blocks { get; }

    [RelayCommand]
    private void OpenLink(string url) => launcher.Open(url);
}
