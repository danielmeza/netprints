using Avalonia.Controls;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// Holds the main window, which owns dialogs and pickers, and closes it on request.
/// </summary>
public sealed class WindowService : IWindowService
{
    /// <summary>The main window (owner of dialogs and pickers).</summary>
    public Window? MainWindow { get; set; }

    /// <summary>The window to own dialogs and pickers.</summary>
    public Window? ActiveWindow => MainWindow;

    /// <inheritdoc/>
    public void CloseMainWindow() => MainWindow?.Close();
}
