using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// Holds the main window and picks the window that owns dialogs and pickers (the active one, so a floated Dock window owns its own), and closes the main window on request.
/// </summary>
public sealed class WindowService : IWindowService
{
    /// <summary>The main window (owner of dialogs and pickers when no other window is active).</summary>
    public Window? MainWindow { get; set; }

    /// <summary>Gets or sets where the open windows come from; by default the desktop lifetime's windows.</summary>
    public Func<IReadOnlyList<Window>> OpenWindows { get; set; } = DesktopWindows;

    /// <summary>The active window of the application (the main window or a Dock host window), or the main window when none is active.</summary>
    public Window? ActiveWindow => OpenWindows().FirstOrDefault(window => window.IsActive) ?? MainWindow;

    /// <inheritdoc/>
    public void CloseMainWindow() => MainWindow?.Close();

    private static IReadOnlyList<Window> DesktopWindows() =>
        global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.Windows : [];
}
