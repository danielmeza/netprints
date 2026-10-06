using System.ComponentModel;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The start page document (<c>start</c>): the registered dashboard tiles, and the error of a startup that failed.</summary>
internal sealed class StartPageViewModel : DocumentViewModel
{
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
    }

    /// <summary>Gets the tile view models in tile order.</summary>
    public IReadOnlyList<object> Tiles { get; }

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
}
