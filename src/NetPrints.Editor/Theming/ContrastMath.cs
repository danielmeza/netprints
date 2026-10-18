using Avalonia.Media;

namespace NetPrints.Editor.Theming;

/// <summary>The WCAG 2.x contrast arithmetic the theme gates use (FR-066): relative luminance, the contrast ratio, and alpha compositing.</summary>
public static class ContrastMath
{
    private const double ChannelMax = 255.0;
    private const double LinearThreshold = 0.03928;
    private const double LinearDivisor = 12.92;
    private const double GammaOffset = 0.055;
    private const double GammaScale = 1.055;
    private const double GammaExponent = 2.4;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double Flare = 0.05;

    /// <summary>Computes the relative luminance of a colour, ignoring its alpha.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The luminance, from 0 (black) to 1 (white).</returns>
    public static double Luminance(Color color) =>
        (RedWeight * Linear(color.R)) + (GreenWeight * Linear(color.G)) + (BlueWeight * Linear(color.B));

    /// <summary>Computes the contrast ratio of two colours, whichever is lighter.</summary>
    /// <param name="a">The first colour.</param>
    /// <param name="b">The second colour.</param>
    /// <returns>The ratio, from 1 (identical) to 21 (black on white).</returns>
    public static double Ratio(Color a, Color b)
    {
        double x = Luminance(a);
        double y = Luminance(b);
        return (Math.Max(x, y) + Flare) / (Math.Min(x, y) + Flare);
    }

    /// <summary>Draws a colour over an opaque one by the alpha of the top colour.</summary>
    /// <param name="top">The colour drawn on top; its alpha sets the mix.</param>
    /// <param name="bottom">The colour underneath, taken as opaque.</param>
    /// <returns>The opaque result.</returns>
    public static Color Composite(Color top, Color bottom)
    {
        double alpha = top.A / ChannelMax;
        byte Mix(byte over, byte under) => (byte)Math.Round((over * alpha) + (under * (1 - alpha)));
        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    /// <summary>Scales the alpha of a colour by an element opacity, as a wire drawn at 0.78 is.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="opacity">The opacity, from 0 to 1.</param>
    /// <returns>The colour with its alpha multiplied by the opacity.</returns>
    public static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B);

    private static double Linear(byte value)
    {
        double c = value / ChannelMax;
        return c <= LinearThreshold ? c / LinearDivisor : Math.Pow((c + GammaOffset) / GammaScale, GammaExponent);
    }
}
