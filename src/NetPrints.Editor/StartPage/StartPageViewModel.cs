using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

namespace NetPrints.Editor.StartPage;

/// <summary>The start page document (<c>start</c>): the registered dashboard tiles, and the error of a startup that failed.</summary>
internal sealed partial class StartPageViewModel : DocumentViewModel
{
    private const int VersionParts = 3;

    private readonly ShellViewModel shell;
    private readonly StartPageContext context;
    private List<(DashboardTileDescriptor Descriptor, object Tile)> built = [];
    private readonly IEditorStateStore? store;

    /// <summary>Creates the page from the registered tiles.</summary>
    /// <param name="shell">The shell, whose registry lists the tiles and whose <c>StartPageError</c> the page shows.</param>
    /// <param name="context">What the tiles' factories are given.</param>
    internal StartPageViewModel(ShellViewModel shell, StartPageContext context)
        : base(DocumentId.StartPage, "Start")
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(context);
        this.shell = shell;
        this.context = context;
        Tiles = BuildTiles(shell.Registry.Snapshot);
        shell.PropertyChanged += OnShellChanged;
        shell.Registry.Changed += OnRegistryChanged;
        if (context.StateStore is { } stateStore)
        {
            ReopenLastProject = stateStore.LoadStart()?.StartupBehavior == StartupBehavior.ReopenLastProject;
            store = stateStore;
            string? seen = stateStore.LoadStart()?.WhatsNewSeenVersion;
            IsWhatsNewExpanded = !string.Equals(seen, ProductVersion, StringComparison.Ordinal);
            if (IsWhatsNewExpanded)
            {
                stateStore.Update(state => state with { WhatsNewSeenVersion = ProductVersion }, userChanged: false);
            }
        }
        else
        {
            IsWhatsNewExpanded = true;
        }
    }

    /// <summary>Gets the tile view models in tile order; the page rebuilds them when the registry changes, keeping the view models of the tiles that stay.</summary>
    public IReadOnlyList<object> Tiles { get; private set; }

    /// <summary>Gets the product version shown beside the title.</summary>
    public static string ProductVersion { get; } = ReadProductVersion();

    /// <summary>Gets the recent tile, or null when no tile of that kind is registered.</summary>
    public RecentProjectsTileViewModel? Recent => Tiles.OfType<RecentProjectsTileViewModel>().FirstOrDefault();

    /// <summary>Gets the action tiles, new project first and then open.</summary>
    public IReadOnlyList<object> GetStarted => [.. Tiles.Where(tile => tile is OpenProjectTileViewModel or NewProjectTileViewModel).OrderBy(tile => tile is NewProjectTileViewModel ? 0 : 1)];

    /// <summary>Gets the samples tile, or null.</summary>
    public SamplesTileViewModel? Samples => Tiles.OfType<SamplesTileViewModel>().FirstOrDefault();

    /// <summary>Gets the Learn tile, or null.</summary>
    public LearnTileViewModel? Learn => Tiles.OfType<LearnTileViewModel>().FirstOrDefault();

    /// <summary>Gets the what's new tile, or null.</summary>
    public WhatsNewTileViewModel? WhatsNew => Tiles.OfType<WhatsNewTileViewModel>().FirstOrDefault();

    /// <summary>Gets the what's new tile as a list of one (or none), so the view builds and tears down its notes as an items host does.</summary>
    public IReadOnlyList<WhatsNewTileViewModel> WhatsNewTiles => [.. Tiles.OfType<WhatsNewTileViewModel>().Take(1)];

    /// <summary>Gets the tiles an extension registered, which the page shows below the get started cards.</summary>
    public IReadOnlyList<object> Others => [.. Tiles.Where(tile => tile is not (RecentProjectsTileViewModel or OpenProjectTileViewModel or NewProjectTileViewModel or SamplesTileViewModel or LearnTileViewModel or WhatsNewTileViewModel))];

    /// <summary>Gets the product version shown beside the title.</summary>
    public string Version => ProductVersion;

    /// <summary>Gets or sets a value indicating whether the what's new section is open.</summary>
    [ObservableProperty]
    public partial bool IsWhatsNewExpanded { get; set; }

    /// <summary>Gets or sets a value indicating whether the editor reopens the last project when it starts instead of showing this page.</summary>
    [ObservableProperty]
    public partial bool ReopenLastProject { get; set; }

    partial void OnReopenLastProjectChanged(bool value) =>
        store?.Update(state => state with { StartupBehavior = value ? StartupBehavior.ReopenLastProject : StartupBehavior.ShowStartPage });

    /// <summary>Opens the what's new section when it is closed and closes it when it is open.</summary>
    [RelayCommand]
    private void ToggleWhatsNew() => IsWhatsNewExpanded = !IsWhatsNewExpanded;

    /// <summary>Gets the error shown above the tiles, or null.</summary>
    public string? Error => shell.StartPageError;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            shell.PropertyChanged -= OnShellChanged;
            shell.Registry.Changed -= OnRegistryChanged;
        }
    }

    private void OnRegistryChanged(object? sender, ContributionSnapshot snapshot)
    {
        Tiles = BuildTiles(snapshot);
        foreach (string name in (string[])[nameof(Tiles), nameof(Recent), nameof(GetStarted), nameof(Samples), nameof(Learn), nameof(WhatsNew), nameof(WhatsNewTiles), nameof(Others)])
        {
            OnPropertyChanged(name);
        }
    }

    private List<object> BuildTiles(IContributionSnapshot snapshot)
    {
        List<(DashboardTileDescriptor Descriptor, object Tile)> next = [];
        foreach (DashboardTileDescriptor descriptor in snapshot.DashboardTiles.OrderBy(tile => tile.Order))
        {
            int index = built.FindIndex(entry => ReferenceEquals(entry.Descriptor, descriptor));
            next.Add(index >= 0 ? built[index] : (descriptor, descriptor.CreateViewModel(context)));
        }

        built = next;
        return [.. next.Select(entry => entry.Tile)];
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
