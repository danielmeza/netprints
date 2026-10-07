using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>The start page document (<c>start</c>): the registered dashboard tiles, and the error of a startup that failed.</summary>
internal sealed partial class StartPageViewModel : DocumentViewModel
{
    private const int VersionParts = 3;

    private readonly ShellViewModel shell;

    /// <summary>Creates the page from the registered tiles.</summary>
    /// <param name="shell">The shell, whose registry lists the tiles and whose <c>StartPageError</c> the page shows.</param>
    /// <param name="services">What the tiles' factories ask for.</param>
    public StartPageViewModel(ShellViewModel shell, IServiceProvider services)
        : base(DocumentId.StartPage, "Start")
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(services);
        this.shell = shell;
        Tiles = [.. shell.Registry.DashboardTiles.OrderBy(tile => tile.Order).Select(tile => tile.CreateViewModel(services))];
        shell.PropertyChanged += OnShellChanged;
        if (services.GetService(typeof(IEditorStateStore)) is IEditorStateStore store)
        {
            string? seen = store.LoadStart()?.WhatsNewSeenVersion;
            IsWhatsNewExpanded = !string.Equals(seen, ProductVersion, StringComparison.Ordinal);
            if (IsWhatsNewExpanded)
            {
                store.SaveStart(new StartState(StateFile.CurrentVersion, ProductVersion));
            }
        }
        else
        {
            IsWhatsNewExpanded = true;
        }
    }

    /// <summary>Gets the tile view models in tile order.</summary>
    public IReadOnlyList<object> Tiles { get; }

    /// <summary>Gets the product version shown beside the title.</summary>
    public static string ProductVersion { get; } = ReadProductVersion();

    /// <summary>Gets the recent tile, or null when no tile of that kind is registered.</summary>
    public RecentProjectsTileViewModel? Recent => Tiles.OfType<RecentProjectsTileViewModel>().FirstOrDefault();

    /// <summary>Gets the action tiles (open, new project) in tile order.</summary>
    public IReadOnlyList<object> GetStarted => [.. Tiles.Where(tile => tile is OpenProjectTileViewModel or NewProjectTileViewModel)];

    /// <summary>Gets the samples tile, or null.</summary>
    public SamplesTileViewModel? Samples => Tiles.OfType<SamplesTileViewModel>().FirstOrDefault();

    /// <summary>Gets the what's new tile, or null.</summary>
    public WhatsNewTileViewModel? WhatsNew => Tiles.OfType<WhatsNewTileViewModel>().FirstOrDefault();

    /// <summary>Gets the tiles an extension registered, which the page shows below the get started cards.</summary>
    public IReadOnlyList<object> Others => [.. Tiles.Where(tile => tile is not (RecentProjectsTileViewModel or OpenProjectTileViewModel or NewProjectTileViewModel or SamplesTileViewModel or WhatsNewTileViewModel))];

    /// <summary>Gets the product version shown beside the title.</summary>
    public string Version => ProductVersion;

    /// <summary>Gets or sets a value indicating whether the what's new section is open.</summary>
    [ObservableProperty]
    public partial bool IsWhatsNewExpanded { get; set; }

    /// <summary>Gets the error shown above the tiles, or null.</summary>
    public string? Error => shell.StartPageError;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            shell.PropertyChanged -= OnShellChanged;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.StartPageError))
        {
            OnPropertyChanged(nameof(Error));
        }
    }

    private static string ReadProductVersion()
    {
        string? informational = typeof(StartPageViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        int plus = informational?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        return informational is { Length: > 0 } ? (plus > 0 ? informational[..plus] : informational) : typeof(StartPageViewModel).Assembly.GetName().Version?.ToString(VersionParts) ?? "0.0.0";
    }
}
