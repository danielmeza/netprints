namespace NetPrints.Editor.State;

/// <summary>
/// Restores and saves the main window's bounds and maximized state (FR-050, FR-051). The window's behavior reads the screens and
/// the window; the placement rule itself is <see cref="WindowPlacement.Resolve"/>.
/// </summary>
/// <param name="store">Where <c>window.json</c> is read from and saved to.</param>
public sealed class WindowStateService(IEditorStateStore store)
{
    /// <summary>Works out where the window opens.</summary>
    /// <param name="screens">The working areas of the current screens, in pixels.</param>
    /// <param name="primary">The working area of the primary screen.</param>
    /// <returns>The placement, or <see langword="null"/> when nothing usable was saved.</returns>
    public WindowPlacement? Restore(IReadOnlyList<ScreenBounds> screens, ScreenBounds primary) =>
        store.LoadWindow() is { } saved ? WindowPlacement.Resolve(saved, screens, primary) : null;

    /// <summary>Saves the window's state.</summary>
    /// <param name="bounds">The normal (not maximized) bounds, in pixels.</param>
    /// <param name="isMaximized">Whether the window is maximized.</param>
    /// <param name="screen">The working area of the screen the window is on, when known.</param>
    public void Save(ScreenBounds bounds, bool isMaximized, ScreenBounds? screen)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        store.SaveWindow(new WindowState(StateFile.CurrentVersion, bounds.X, bounds.Y, bounds.Width, bounds.Height, isMaximized, screen));
    }
}
