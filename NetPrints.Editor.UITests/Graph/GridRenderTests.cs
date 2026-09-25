using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
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

    /// <summary>
    /// Two grids side by side, at zoom 1 (minor lines at full alpha) and 0.37 (minor lines half faded),
    /// panned to fractional graph points and placed at a fractional offset.
    /// </summary>
    private static (Window Window, GridBackground[] Grids) CreateWindow(GridRenderMode mode)
    {
        GridBackground Grid(double zoom, Point location) => new()
        {
            Mode = mode,
            ViewportZoom = zoom,
            ViewportLocation = location,
            Width = 300,
            Height = 200,
            Margin = new Thickness(7.3, 3.6, 0, 0),
        };

        var grids = new[] { Grid(1, new Point(-123.4, 56.7)), Grid(0.37, new Point(1_000_000.37, -2_000_000.5)) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.AddRange(grids);
        var window = new Window { Width = 640, Height = 220, Content = panel };
        return (window, grids);
    }

    private static async Task<UiImage> RenderAsync(GridRenderMode mode, GridRenderPath expectedPath)
    {
        using var ui = HeadlessUi.Create();
        var (window, grids) = CreateWindow(mode);
        ui.Show(window);
        var image = await ui.Driver.ScreenshotAsync(ui.Tree.KeyOf(window), Token);
        Assert.All(grids, g => Assert.Equal(expectedPath, g.LastRenderPath));
        return image;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ShaderAndCpuPathsDrawIdenticalPixels()
    {
        var shader = await RenderAsync(GridRenderMode.Shader, GridRenderPath.Shader);
        var cpu = await RenderAsync(GridRenderMode.Cpu, GridRenderPath.Cpu);

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
        UiArtifacts.Snapshots.Match("grid-background", cpu, exact);
        UiArtifacts.Snapshots.Match("grid-background", shader, exact);
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
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;

        await graph.RightDragAsync(100, 60, Token);
        await graph.WheelAsync(await graph.EmptyPointAsync(Token, -300, -300), -1, Token);

        var (x, y) = await graph.ViewportAsync(Token);
        Assert.Equal((x, y, await graph.ZoomAsync(Token)), await graph.GridViewportAsync(Token));
        Assert.Equal("Cpu", await graph.GridRenderPathAsync(Token));
        Assert.Equal(("#ff252525", "#14ffffff", "#2effffff"), await graph.GridColorsAsync(Token)); // dark theme resources

        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        try
        {
            Assert.Equal(("#fff3f3f3", "#14000000", "#2e000000"), await graph.GridColorsAsync(Token));
        }
        finally
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
}
