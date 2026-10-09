using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Pin rows without a background, the type-pin shape token, the pin hover outline and the canvas background (FR-109, T092m).</summary>
public class CanvasDetailTests
{
    private const int ChannelTolerance = 2;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UiImage FrameOf(Window window)
    {
        HeadlessDriver.Pump();
        using WriteableBitmap frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
        using var stream = new MemoryStream();
        frame.Save(stream, new PngBitmapEncoderOptions());
        return new UiImage(stream.ToArray());
    }

    private static Point OriginIn(Visual visual, Visual window) =>
        visual.TranslatePoint(new Point(0, 0), window) ?? throw new InvalidOperationException("The control is not in the window.");

    private static void AssertSamePixel(UiImage frame, int ax, int ay, int bx, int by, string what)
    {
        uint a = frame.Pixel(ax, ay);
        uint b = frame.Pixel(bx, by);
        for (int shift = 0; shift <= 16; shift += 8)
        {
            int channelA = (int)((a >> shift) & 0xFF);
            int channelB = (int)((b >> shift) & 0xFF);
            Assert.True(Math.Abs(channelA - channelB) <= ChannelTolerance, $"{what}: {a:X8} at ({ax},{ay}) against {b:X8} at ({bx},{by})");
        }
    }

    private static NodeView ViewOf(EditorSession session, NetPrints.Graph.Node node) =>
        session.Window.GetVisualDescendants().OfType<NodeView>().Single(v => CanvasPaletteTests.ModelOf(v).Node == node);

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task APinRowDrawsNothingBehindItsPinAndLabel(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            NodeView view = session.Window.GetVisualDescendants().OfType<NodeView>().Single(v => CanvasPaletteTests.ModelOf(v).Node.Name == "CallMethodNode");
            UiImage frame = FrameOf(session.Window);

            var inputs = view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeInput>().Where(i => i.IsEffectivelyVisible).ToList();
            var outputs = view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeOutput>().Where(o => o.IsEffectivelyVisible).ToList();
            Assert.NotEmpty(inputs);
            Assert.NotEmpty(outputs);

            foreach (var input in inputs)
            {
                Point origin = OriginIn(input, session.Window);
                AssertSamePixel(frame, (int)origin.X + 1, (int)origin.Y + 1, (int)origin.X - 3, (int)origin.Y + 1, $"input {input.DataContext} in {variant}");
            }

            foreach (var output in outputs)
            {
                Point origin = OriginIn(output, session.Window);
                int right = (int)(origin.X + output.Bounds.Width);
                AssertSamePixel(frame, right - 2, (int)origin.Y + 1, right + 2, (int)origin.Y + 1, $"output {output.DataContext} in {variant}");
            }
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task EveryTypePinDrawsThePinTypeShapeGeometry(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var node = session.GraphViewModel.AddNode<TypeNode>(new GraphPoint(28, 280), null, TypeSpecifier.FromType<string>());
        await session.WaitForRenderedAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            object? shape = CanvasPaletteTests.Resolve("Pin.TypeShape", CanvasPaletteTests.VariantOf(variant));
            Geometry expected = Assert.IsAssignableFrom<Geometry>(shape);
            Assert.IsAssignableFrom<Geometry>(CanvasPaletteTests.Resolve("Pin.TypeShape.Triangle", CanvasPaletteTests.VariantOf(variant)));
            Assert.IsAssignableFrom<Geometry>(CanvasPaletteTests.Resolve("Pin.TypeShape.Diamond", CanvasPaletteTests.VariantOf(variant)));

            var typePins = ViewOf(session, node).GetVisualDescendants().OfType<Shape>().Where(s => s.Classes.Contains("pin") && s.Classes.Contains("pin-type")).ToList();

            Assert.NotEmpty(typePins);
            Assert.All(typePins, pin =>
            {
                var path = Assert.IsType<Avalonia.Controls.Shapes.Path>(pin);
                Assert.Same(expected, path.Data);
            });
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task HoveringAPinOutlinesItInTheHoverStroke(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            NodeView view = session.Window.GetVisualDescendants().OfType<NodeView>().Single(v => CanvasPaletteTests.ModelOf(v).Node.Name == "CallMethodNode");
            Panel hitbox = view.GetVisualDescendants().OfType<Panel>().First(p => p.Classes.Contains("pinHitbox") && p.IsEffectivelyVisible);
            Shape shape = hitbox.GetVisualDescendants().OfType<Shape>().First(s => s.Classes.Contains("pin") && s.IsEffectivelyVisible);
            Color hover = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Pin.HoverStroke", CanvasPaletteTests.VariantOf(variant)));

            session.Window.MouseMove(new Point(0, 0), RawInputModifiers.None);
            HeadlessDriver.Pump();
            Assert.False(shape.Stroke is ISolidColorBrush before && before.Color == hover, "not outlined while the pointer is elsewhere");

            Point center = OriginIn(hitbox, session.Window) + new Point(hitbox.Bounds.Width / 2, hitbox.Bounds.Height / 2);
            session.Window.MouseMove(center, RawInputModifiers.None);
            HeadlessDriver.Pump();

            Assert.True(shape.IsPointerOver);
            Assert.True(shape.Stroke is ISolidColorBrush stroke && stroke.Color == hover, $"stroke of {shape.GetType().Name} under the pointer: {shape.Stroke}");
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task TheGridDrawsTheCanvasBackgroundToken(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            Color token = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Canvas.Background", CanvasPaletteTests.VariantOf(variant)));
            Color today = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("SystemRegionColor", CanvasPaletteTests.VariantOf(variant)));
            GridBackground grid = session.Window.GetVisualDescendants().OfType<GridBackground>().Single();

            Assert.Equal(today, token);
            Assert.Equal(token, grid.BackgroundColor);
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }
}
