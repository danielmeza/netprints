namespace NetPrints.Editor.State;

/// <summary>Where the main window goes when it opens.</summary>
/// <param name="Bounds">The normal bounds, in pixels.</param>
/// <param name="IsMaximized">Whether the window opens maximized.</param>
/// <param name="Scaling">The scaling of the screen the window lands on, to turn its pixel size into device-independent pixels.</param>
public sealed record WindowPlacement(ScreenBounds Bounds, bool IsMaximized, double Scaling = 1.0)
{
    /// <summary>Gets the width in device-independent pixels, at the scaling of the target screen.</summary>
    public double WidthInDips => Bounds.Width / Scaling;

    /// <summary>Gets the height in device-independent pixels, at the scaling of the target screen.</summary>
    public double HeightInDips => Bounds.Height / Scaling;

    /// <summary>
    /// Decides where a saved window goes (state-files.md §3): on the screen its bounds overlap most, clamped to that screen's
    /// size with its title strip reachable; otherwise centred on the primary screen at its saved size, clamped to that screen. The maximized state is kept either way.
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

        ScreenBounds? target = screens.Where(screen => Overlap(screen, saved.Bounds) > 0).MaxBy(screen => Overlap(screen, saved.Bounds));
        if (target is not null)
        {
            return new WindowPlacement(Clamp(saved.Bounds, target), saved.IsMaximized, target.Scaling);
        }

        int width = Math.Min(saved.Width, primary.Width);
        int height = Math.Min(saved.Height, primary.Height);
        return new WindowPlacement(new ScreenBounds(primary.X + Margin(primary.Width, width), primary.Y + Margin(primary.Height, height), width, height), saved.IsMaximized, primary.Scaling);
    }

    private const int Halves = 2;
    private const int TitleStripHeight = 32;
    private const int TitleStripWidth = 100;

    private static long Overlap(ScreenBounds screen, ScreenBounds window)
    {
        long width = Math.Min(screen.Right, window.Right) - Math.Max(screen.X, window.X);
        long height = Math.Min(screen.Bottom, window.Bottom) - Math.Max(screen.Y, window.Y);
        return width > 0 && height > 0 ? width * height : 0;
    }

    // Fits the size to the screen, then keeps a title strip on it: the top edge on the screen, and at least 100 px of the width.
    private static ScreenBounds Clamp(ScreenBounds window, ScreenBounds screen)
    {
        int width = Math.Min(window.Width, screen.Width);
        int height = Math.Min(window.Height, screen.Height);
        int x = width < window.Width ? screen.X : Math.Clamp(window.X, screen.X + Math.Min(TitleStripWidth, width) - width, screen.Right - Math.Min(TitleStripWidth, width));
        int y = height < window.Height ? screen.Y : Math.Clamp(window.Y, screen.Y, screen.Bottom - Math.Min(TitleStripHeight, height));
        return new ScreenBounds(x, y, width, height);
    }

    private static int Margin(int outer, int inner) => (outer - inner) / Halves;
}
