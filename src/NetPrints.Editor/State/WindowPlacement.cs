namespace NetPrints.Editor.State;

/// <summary>Where the main window goes when it opens.</summary>
/// <param name="Bounds">The normal bounds, in pixels.</param>
/// <param name="IsMaximized">Whether the window opens maximized.</param>
public sealed record WindowPlacement(ScreenBounds Bounds, bool IsMaximized)
{
    /// <summary>
    /// Decides where a saved window goes (state-files.md §3): where it was when its bounds still touch a screen; otherwise
    /// centred on the primary screen at its saved size, clamped to that screen. The maximized state is kept either way.
    /// </summary>
    /// <param name="saved">The saved state.</param>
    /// <param name="screens">The working areas of the current screens.</param>
    /// <param name="primary">The working area of the primary screen.</param>
    /// <returns>The placement, or <see langword="null"/> when the saved size is not usable and the window keeps its default.</returns>
    public static WindowPlacement? Resolve(WindowState saved, IReadOnlyList<ScreenBounds> screens, ScreenBounds primary)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(primary);
        if (saved.Width <= 0 || saved.Height <= 0 || primary.Width <= 0 || primary.Height <= 0)
        {
            return null;
        }

        if (screens.Any(screen => screen.Intersects(saved.Bounds)))
        {
            return new WindowPlacement(saved.Bounds, saved.IsMaximized);
        }

        int width = Math.Min(saved.Width, primary.Width);
        int height = Math.Min(saved.Height, primary.Height);
        return new WindowPlacement(new ScreenBounds(primary.X + Margin(primary.Width, width), primary.Y + Margin(primary.Height, height), width, height), saved.IsMaximized);
    }

    private const int Halves = 2;

    private static int Margin(int outer, int inner) => (outer - inner) / Halves;
}
