using Avalonia.Media;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The look of the graph canvas grid: the single definition read by both render paths of
/// <see cref="GridBackground"/> (the SkSL shader and the CPU path), so they draw the same pixels.
/// </summary>
/// <param name="CellSize">Minor cell size in graph units (<see cref="GraphConstants.GridCellSize"/>).</param>
/// <param name="MajorEvery">A major line every this many cells.</param>
/// <param name="MinorWidth">Minor line width in DIPs; rounded to whole device pixels, at least one.</param>
/// <param name="MajorWidth">Major line width in DIPs; rounded to whole device pixels, at least one.</param>
/// <param name="BackgroundColor">Canvas background color, painted by the grid; always opaque (its alpha is ignored), because both render paths write pixels without blending.</param>
/// <param name="MinorColor">Minor line color (straight alpha), composited over the background.</param>
/// <param name="MajorColor">Major line color (straight alpha), composited over the background.</param>
/// <param name="MinorFadeOut">On-screen cell size in DIPs at and below which minor lines are hidden.</param>
/// <param name="MinorFadeIn">On-screen cell size in DIPs at and above which minor lines are fully visible.</param>
public sealed record GridStyle(
    double CellSize,
    int MajorEvery,
    double MinorWidth,
    double MajorWidth,
    Color BackgroundColor,
    Color MinorColor,
    Color MajorColor,
    double MinorFadeOut = 6,
    double MinorFadeIn = 14);

/// <summary>
/// Everything one frame of the grid needs, in device pixels. Computed once per frame in double
/// precision by <see cref="Compute"/>; both render paths only read it.
/// </summary>
/// <remarks>
/// The colors are final pixel colors: each line color is composited over the background here, so
/// the render paths only write these colors (major lines win at crossings) and never blend. That
/// keeps the paths pixel-identical on every backend, including Skia's raster pipeline, whose
/// low-precision blending rounds differently from a shader's.
/// </remarks>
/// <param name="PhaseX">Device x of a major vertical line, reduced into [0, one major period).</param>
/// <param name="PhaseY">Device y of a major horizontal line, reduced into [0, one major period).</param>
/// <param name="Cell">Minor cell size in device pixels.</param>
/// <param name="MajorEvery">A major line every this many cells.</param>
/// <param name="MinorWidth">Minor line width in whole device pixels.</param>
/// <param name="MajorWidth">Major line width in whole device pixels.</param>
/// <param name="MinorAlpha">The minor line alpha after the level-of-detail fade; 0 hides minor lines.</param>
/// <param name="BackgroundColor">The color of pixels without a line.</param>
/// <param name="MinorColor">The color of minor-line pixels (the faded minor color over the background).</param>
/// <param name="MajorColor">The color of major-line pixels (the major color over the background).</param>
public readonly record struct GridFrame(
    float PhaseX,
    float PhaseY,
    float Cell,
    int MajorEvery,
    int MinorWidth,
    int MajorWidth,
    byte MinorAlpha,
    Color BackgroundColor,
    Color MinorColor,
    Color MajorColor)
{
    /// <summary>
    /// Derives the frame parameters. The phase is taken modulo one major period, so the floats the
    /// render paths use stay small however far the view is panned, and line index 0 of the reduced
    /// phase is always a major line. Minor lines fade with a smoothstep between
    /// <see cref="GridStyle.MinorFadeOut"/> and <see cref="GridStyle.MinorFadeIn"/> instead of popping.
    /// </summary>
    /// <param name="style">The grid look.</param>
    /// <param name="locationX">Viewport location x in graph units (graph point at the control's left edge).</param>
    /// <param name="locationY">Viewport location y in graph units (graph point at the control's top edge).</param>
    /// <param name="zoom">Viewport zoom (DIPs per graph unit).</param>
    /// <param name="scale">Device pixels per DIP (render scaling times any ancestor scale).</param>
    /// <param name="originX">Device x of the control's left edge.</param>
    /// <param name="originY">Device y of the control's top edge.</param>
    public static GridFrame Compute(GridStyle style, double locationX, double locationY, double zoom, double scale,
        double originX, double originY)
    {
        double cellDip = style.CellSize * zoom;
        double cell = cellDip * scale;
        double period = cell * style.MajorEvery;
        byte minorAlpha = (byte)Math.Round(style.MinorColor.A * SmoothStep(style.MinorFadeOut, style.MinorFadeIn, cellDip));
        var minor = Color.FromArgb(minorAlpha, style.MinorColor.R, style.MinorColor.G, style.MinorColor.B);
        var background = Color.FromRgb(style.BackgroundColor.R, style.BackgroundColor.G, style.BackgroundColor.B);

        return new GridFrame(
            (float)Mod(originX - locationX * zoom * scale, period),
            (float)Mod(originY - locationY * zoom * scale, period),
            (float)cell,
            style.MajorEvery,
            DeviceWidth(style.MinorWidth, scale),
            DeviceWidth(style.MajorWidth, scale),
            minorAlpha,
            background,
            Over(minor, background),
            Over(style.MajorColor, background));
    }

    /// <summary>The first device column (or row) covered by a line centered at <paramref name="center"/>.</summary>
    /// <remarks>The snapping rule shared by both render paths.</remarks>
    public static float SnapStart(float center, int width) => MathF.Floor(center + 0.5f - 0.5f * width);

    /// <summary>Whether line <paramref name="index"/> (0 at the phase) is a major line.</summary>
    public bool IsMajor(int index) => ((index % MajorEvery) + MajorEvery) % MajorEvery == 0;

    /// <summary>Source-over compositing of straight-alpha colors, rounded to bytes.</summary>
    public static Color Over(Color source, Color destination)
    {
        double sa = source.A / 255.0, da = destination.A / 255.0;
        double a = sa + da * (1 - sa);
        if (a <= 0)
        {
            return Color.FromArgb(0, 0, 0, 0);
        }

        byte Channel(byte s, byte d) => (byte)Math.Round((s * sa + d * da * (1 - sa)) / a);
        return Color.FromArgb((byte)Math.Round(a * 255), Channel(source.R, destination.R), Channel(source.G, destination.G),
            Channel(source.B, destination.B));
    }

    private static int DeviceWidth(double dip, double scale) => Math.Max(1, (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero));

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Mod(double value, double modulus) => value - modulus * Math.Floor(value / modulus);
}
