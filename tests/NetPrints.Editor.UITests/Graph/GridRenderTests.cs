using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>The canvas grid (spec 002): both render paths draw the same pixels, and match the baseline.</summary>
public class GridRenderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const double Left = 7.3, Top = 3.6, GridWidth = 300, GridHeight = 200;

    /// <summary>
    /// Two grids side by side, at zoom 1 (minor lines at full alpha) and 0.37 (minor lines half
    /// faded), panned to fractional graph points and placed at fractional device offsets.
    /// </summary>
    private static (Window Window, GridBackground[] Grids) CreateWindow(GridRenderMode mode)
    {
        GridBackground Grid(double zoom, Point location) => new()
        {
            Mode = mode,
            ViewportZoom = zoom,
            ViewportLocation = location,
            Width = GridWidth,
            Height = GridHeight,
            Margin = new Thickness(Left, Top, 0, 0),
            UseLayoutRounding = false,
        };

        var grids = new[] { Grid(1, new Point(-123.4, 56.7)), Grid(0.37, new Point(1_000_000.37, -2_000_000.5)) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, UseLayoutRounding = false };
        panel.Children.AddRange(grids);
        var window = new Window { Width = 640, Height = 220, Content = panel };
        return (window, grids);
    }

    private static async Task<UiImage> RenderAsync(GridRenderMode mode, GridRenderPath expectedPath, double scaling = 1)
    {
        using var ui = HeadlessUi.Create();
        var (window, grids) = CreateWindow(mode);
        ui.Show(window);
        window.SetRenderScaling(scaling);
        var image = await ui.Driver.ScreenshotAsync(ui.Tree.KeyOf(window), Token);

        for (int i = 0; i < grids.Length; i++)
        {
            var frame = grids[i].LastFrame;
            Assert.NotNull(frame);
            Assert.Equal(expectedPath, frame.Path);
            Assert.Equal(scaling, frame.Scale, 6);
            Assert.Equal((Left + i * (GridWidth + Left)) * scaling, frame.OriginX, 3); // fractional: not snapped by layout
            Assert.Equal(Top * scaling, frame.OriginY, 3);
        }

        return image;
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(1.0, "grid-background")]
    [InlineData(1.5, "grid-background-150")] // 2-px lines
    public async Task ShaderAndCpuPathsDrawIdenticalPixels(double scaling, string baseline)
    {
        var shader = await RenderAsync(GridRenderMode.Shader, GridRenderPath.Shader, scaling);
        var cpu = await RenderAsync(GridRenderMode.Cpu, GridRenderPath.Cpu, scaling);

        Assert.Equal((cpu.Width, cpu.Height), (shader.Width, shader.Height));
        int differing = 0;
        for (int y = 0; y < cpu.Height; y++)
        {
            for (int x = 0; x < cpu.Width; x++)
            {
                differing += cpu.Pixel(x, y) == shader.Pixel(x, y) ? 0 : 1;
            }
        }

        Assert.Equal(0, differing);
        var exact = new SnapshotOptions { PixelThreshold = 0, MaxDiffPercent = 0 };
        UiArtifacts.Snapshots.Match(baseline, cpu, exact);
        UiArtifacts.Snapshots.Match(baseline, shader, exact);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AutoUsesTheCpuPathWithoutAGpu()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable(GridBackground.ModeVariable) is { Length: > 0 },
            $"{GridBackground.ModeVariable} overrides Auto");

        await RenderAsync(GridRenderMode.Auto, GridRenderPath.Cpu); // headless Skia is the raster backend
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheCanvasGridFollowsTheViewportAndTheTheme()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable(GridBackground.ModeVariable) is { Length: > 0 },
            $"{GridBackground.ModeVariable} overrides Auto");
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;

        await graph.RightDragAsync(100, 60, Token);
        await graph.WheelAsync(await graph.EmptyPointAsync(Token, -300, -300), -1, Token);

        var (x, y) = await graph.ViewportAsync(Token);
        Assert.Equal((x, y, await graph.ZoomAsync(Token)), await graph.GridViewportAsync(Token));
        Assert.Equal("Cpu", await graph.GridRenderPathAsync(Token));
        Assert.Equal((RegionColor(ThemeVariant.Dark), "#14ffffff", "#2effffff"), await graph.GridColorsAsync(Token));
        Assert.Equal(RegionColor(ThemeVariant.Dark), BackgroundOf(await graph.ScreenshotAsync(Token)));

        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        try
        {
            Assert.Equal((RegionColor(ThemeVariant.Light), "#14000000", "#2e000000"), await graph.GridColorsAsync(Token));
            Assert.Equal(RegionColor(ThemeVariant.Light), BackgroundOf(await graph.ScreenshotAsync(Token))); // a new frame was drawn
        }
        finally
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    private static string RegionColor(ThemeVariant theme) =>
        Application.Current!.TryGetResource("SystemRegionColor", theme, out var value) && value is Color color
            ? color.ToString()
            : throw new InvalidOperationException("No SystemRegionColor");

    /// <summary>The most common pixel color of a canvas screenshot (the background), as #aarrggbb.</summary>
    private static string BackgroundOf(UiImage image)
    {
        var counts = new Dictionary<uint, int>();
        for (int y = 0; y < image.Height; y += 3)
        {
            for (int x = 0; x < image.Width; x += 3)
            {
                uint pixel = image.Pixel(x, y);
                counts[pixel] = counts.GetValueOrDefault(pixel) + 1;
            }
        }

        return Color.FromUInt32(counts.MaxBy(c => c.Value).Key).ToString();
    }
}
