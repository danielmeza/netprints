using Avalonia.Media;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Tests.Graph;

public class GridFrameTests
{
    private static readonly GridStyle Style = new(28, 8, MinorWidth: 1, MajorWidth: 1, Color.FromRgb(0x25, 0x25, 0x25),
        Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));

    private static GridFrame Compute(double x = 0, double y = 0, double zoom = 1, double scale = 1,
        double originX = 0, double originY = 0, GridStyle? style = null) =>
        GridFrame.Compute(style ?? Style, x, y, zoom, scale, originX, originY);

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.25, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.0, 2)]
    [InlineData(2.5, 3)]
    public void LineWidthsAreWholeDevicePixels(double scale, int expected)
    {
        var frame = Compute(scale: scale);

        Assert.Equal(expected, frame.MinorWidth);
        Assert.Equal(expected, frame.MajorWidth);
    }

    [Fact]
    public void LinesAreAtLeastOneDevicePixelWide()
    {
        var frame = Compute(scale: 1, style: Style with { MinorWidth = 0.2, MajorWidth = 0.4 });

        Assert.Equal(1, frame.MinorWidth);
        Assert.Equal(1, frame.MajorWidth);
    }

    [Theory]
    [InlineData(0.0, 1, 0f)]      // a 1-px line covers the pixel its centre falls in
    [InlineData(0.99, 1, 0f)]
    [InlineData(1.0, 1, 1f)]
    [InlineData(10.2, 2, 9f)]
    [InlineData(10.6, 2, 10f)]
    [InlineData(-3.7, 1, -4f)]
    public void SnappingCoversWholePixels(double center, int width, float expectedStart)
    {
        Assert.Equal(expectedStart, GridFrame.SnapStart((float)center, width));
    }

    [Fact]
    public void CellAndPhaseAreInDevicePixels()
    {
        var frame = Compute(x: 10, y: -5, zoom: 0.5, scale: 2, originX: 3, originY: 7);

        Assert.Equal(28f, frame.Cell);                           // 28 units * 0.5 zoom * 2 scale
        Assert.Equal(3 - 10 * 0.5 * 2, frame.PhaseX - 28 * 8, 3); // origin - location*zoom*scale, reduced into [0, 224)
        Assert.Equal(7 + 5 * 0.5 * 2, frame.PhaseY, 3);
    }

    [Theory]
    [InlineData(1_000_000.37, -2_000_000.0)]
    [InlineData(-123_456_789.5, 987_654_321.25)]
    public void PhaseStaysSmallAndKeepsTheMajorLineAtLargePanOffsets(double x, double y)
    {
        const double zoom = 0.61, scale = 1.5;
        var frame = Compute(x: x, y: y, zoom: zoom, scale: scale);
        double period = 28 * zoom * scale * 8;

        Assert.InRange(frame.PhaseX, 0f, (float)period);
        Assert.InRange(frame.PhaseY, 0f, (float)period);

        // The phase is the device position of a graph x (and y) that is a multiple of a major period.
        double graphX = x + frame.PhaseX / (zoom * scale);
        double graphY = y + frame.PhaseY / (zoom * scale);
        Assert.Equal(0, Remainder(graphX, 28 * 8), 2);
        Assert.Equal(0, Remainder(graphY, 28 * 8), 2);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(8, true)]
    [InlineData(-8, true)]
    [InlineData(-16, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    [InlineData(7, false)]
    public void EveryEighthLineIsMajor(int index, bool major)
    {
        Assert.Equal(major, Compute().IsMajor(index));
    }

    [Theory]
    [InlineData(1.0, 0x14)]           // 28 DIP cells: full
    [InlineData(0.5, 0x14)]           // 14 DIP: full
    [InlineData(10.0 / 28, 0x0A)]     // 10 DIP: halfway, smoothstep(0.5) = 0.5
    [InlineData(0.3, 0x04)]           // 8.4 DIP: smoothstep(0.3) = 0.216
    [InlineData(6.0 / 28, 0x00)]      // 6 DIP: hidden
    [InlineData(0.1, 0x00)]
    public void MinorLinesFadeBetween14And6Dips(double zoom, int expectedAlpha)
    {
        var frame = Compute(zoom: zoom, scale: 2); // the fade uses DIPs, not device pixels

        Assert.Equal(expectedAlpha, frame.MinorAlpha);
        Assert.Equal(GridFrame.Over(Color.FromArgb((byte)expectedAlpha, 0xFF, 0xFF, 0xFF), Style.BackgroundColor), frame.MinorColor);
        Assert.Equal(GridFrame.Over(Style.MajorColor, Style.BackgroundColor), frame.MajorColor);
    }

    [Fact]
    public void LineColorsAreCompositedOverTheBackground()
    {
        var frame = Compute();

        Assert.Equal(Style.BackgroundColor, frame.BackgroundColor);
        Assert.Equal(Color.FromRgb(0x36, 0x36, 0x36), frame.MinorColor); // 255 * 20/255 + 37 * 235/255 = 54.1
        Assert.Equal(Color.FromRgb(0x4C, 0x4C, 0x4C), frame.MajorColor); // 255 * 46/255 + 37 * 209/255 = 76.3
    }

    [Fact]
    public void TheBackgroundIsAlwaysOpaque()
    {
        var frame = Compute(style: Style with { BackgroundColor = Color.FromArgb(0x40, 0x25, 0x25, 0x25) });

        Assert.Equal(Color.FromRgb(0x25, 0x25, 0x25), frame.BackgroundColor);
        Assert.Equal(Color.FromRgb(0x36, 0x36, 0x36), frame.MinorColor);
    }

    [Fact]
    public void OverHandlesTranslucentBackgrounds()
    {
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x00, 0x00), GridFrame.Over(Color.FromArgb(0x80, 0xFF, 0, 0), Colors.Transparent));
        Assert.Equal(Color.FromArgb(0, 0, 0, 0), GridFrame.Over(Colors.Transparent, Colors.Transparent));
    }

    [Theory]
    [InlineData(GridRenderMode.Auto, GridRenderMode.Auto, GridRenderMode.Auto)]
    [InlineData(GridRenderMode.Auto, GridRenderMode.Cpu, GridRenderMode.Cpu)]
    [InlineData(GridRenderMode.Auto, GridRenderMode.Shader, GridRenderMode.Shader)]
    [InlineData(GridRenderMode.Shader, GridRenderMode.Cpu, GridRenderMode.Shader)]
    [InlineData(GridRenderMode.Cpu, GridRenderMode.Shader, GridRenderMode.Cpu)]
    public void TheEnvironmentOverridesOnlyAuto(GridRenderMode mode, GridRenderMode environment, GridRenderMode expected)
    {
        Assert.Equal(expected, GridBackground.ResolveMode(mode, environment));
    }

    [Theory]
    [InlineData(null, GridRenderMode.Auto)]
    [InlineData("", GridRenderMode.Auto)]
    [InlineData("cpu", GridRenderMode.Cpu)]
    [InlineData(" CPU ", GridRenderMode.Cpu)]
    [InlineData("Shader", GridRenderMode.Shader)]
    [InlineData("gpu", GridRenderMode.Auto)]
    public void ParsesTheEnvironmentVariable(string? value, GridRenderMode expected)
    {
        Assert.Equal(expected, GridBackground.ParseMode(value));
    }

    [Theory]
    [InlineData(GridRenderMode.Auto, true, true, true)]
    [InlineData(GridRenderMode.Auto, false, true, false)] // never the shader on the raster backend in Auto
    [InlineData(GridRenderMode.Auto, true, false, false)] // compile failure
    [InlineData(GridRenderMode.Shader, false, true, true)]
    [InlineData(GridRenderMode.Shader, true, false, false)]
    [InlineData(GridRenderMode.Cpu, true, true, false)]
    public void ChoosesThePath(GridRenderMode mode, bool gpu, bool compiled, bool shader)
    {
        Assert.Equal(shader, GridBackground.UsesShader(mode, gpu, () => compiled));
    }

    [Fact]
    public void TheCpuModeNeverProbesTheShader()
    {
        Assert.False(GridBackground.UsesShader(GridRenderMode.Cpu, hasGpuContext: true,
            () => throw new InvalidOperationException("the shader must not be compiled in Cpu mode")));
    }

    [Fact]
    public void AShaderCompilerThatThrowsMeansTheShaderIsUnavailable()
    {
        var (effect, errors) = GridRenderer.Compile((string source, out string errors) =>
            throw new DllNotFoundException("libSkiaSharp mismatch"));

        Assert.Null(effect);
        Assert.Contains("libSkiaSharp mismatch", errors);
    }

    [Fact]
    public void TheGridShaderCompiles()
    {
        var (effect, errors) = GridRenderer.Compile(SkiaSharp.SKRuntimeEffect.CreateShader);

        Assert.True(effect is not null, errors);
        effect.Dispose();
    }

    private static double Remainder(double value, double modulus)
    {
        double r = value - modulus * Math.Round(value / modulus);
        return Math.Abs(r);
    }
}
