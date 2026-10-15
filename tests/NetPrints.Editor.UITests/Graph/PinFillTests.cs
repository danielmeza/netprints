using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Editor.UITests.Theming;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>An unconnected pin is an outline in its family colour and a connected one is filled with it (FR-108, T092o).</summary>
public class PinFillTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Shape ShapeOf(EditorSession session, NodePinViewModel pin) =>
        session.Window.GetVisualDescendants().OfType<Shape>()
            .Single(shape => shape.Classes.Contains("pin") && shape.IsEffectivelyVisible && ReferenceEquals(shape.DataContext, pin));

    private static NodePinViewModel PinOf(EditorSession session, NodePin pin) =>
        session.GraphViewModel.Nodes.SelectMany(node => node.AllPins).Single(p => p.Pin == pin);

    private static void AssertHollow(Shape shape, NodePinViewModel pin, string variant)
    {
        Avalonia.Styling.ThemeVariant theme = CanvasPaletteTests.VariantOf(variant);
        Color colour = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve(pin.Family.TokenKey, theme));
        double thickness = Assert.IsType<double>(CanvasPaletteTests.Resolve("Pin.OutlineThickness", theme));

        Assert.True(shape.Fill is null || CanvasPaletteTests.ColorOf(shape.Fill).A == 0, $"{pin.Pin.Name} is hollow, fill {shape.Fill}");
        Assert.Equal(colour, CanvasPaletteTests.ColorOf(shape.Stroke));
        Assert.Equal(thickness, shape.StrokeThickness);
        Assert.Equal(1.0, shape.Opacity);
        Assert.DoesNotContain("dimmed", shape.Classes);
    }

    private static void AssertFilled(Shape shape, NodePinViewModel pin, string variant)
    {
        Color colour = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve(pin.Family.TokenKey, CanvasPaletteTests.VariantOf(variant)));

        Assert.NotNull(shape.Fill);
        Assert.Equal(colour, CanvasPaletteTests.ColorOf(shape.Fill));
        Assert.Equal(1.0, shape.Opacity);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task UnconnectedExecutionDataAndTypePinsAreHollowAndConnectedOnesAreFilled(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.GraphViewModel;
        var call = graph.Nodes.Single(n => n.Node is CallMethodNode);
        var typeNode = Assert.IsType<TypeNode>(graph.AddNode<TypeNode>(new GraphPoint(28, 280), null, TypeSpecifier.FromType<string>()));
        var ternary = Assert.IsType<TernaryNode>(graph.AddNode<TernaryNode>(new GraphPoint(300, 280)));
        await session.WaitForRenderedAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            AssertFilled(ShapeOf(session, call.InputExecPins.Single()), call.InputExecPins.Single(), variant);
            AssertHollow(ShapeOf(session, call.OutputExecPins.Single(p => p.Pin.Name == "Catch")), call.OutputExecPins.Single(p => p.Pin.Name == "Catch"), variant);
            AssertHollow(ShapeOf(session, call.InputDataPins.Single()), call.InputDataPins.Single(), variant);
            var typeOut = PinOf(session, typeNode.OutputTypePins[0]);
            AssertHollow(ShapeOf(session, typeOut), typeOut, variant);

            Assert.True(graph.Connect(typeOut, PinOf(session, ternary.TypePin)));
            await session.WaitForRenderedAsync(Token);
            AssertFilled(ShapeOf(session, typeOut), typeOut, variant);
            AssertFilled(ShapeOf(session, PinOf(session, ternary.TypePin)), PinOf(session, ternary.TypePin), variant);
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ConnectingDisconnectingUndoAndRedoSwitchTheFill()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.GraphViewModel;
        var write = graph.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var literal = Assert.IsType<LiteralNode>(graph.AddNode<LiteralNode>(new GraphPoint(28, 280), null, TypeSpecifier.FromType<string>()));
        await session.WaitForRenderedAsync(Token);
        var value = PinOf(session, literal.ValuePin);
        AssertHollow(ShapeOf(session, write), write, "Dark");

        Assert.True(graph.Connect(value, write));
        await session.WaitForRenderedAsync(Token);
        AssertFilled(ShapeOf(session, write), write, "Dark");
        AssertFilled(ShapeOf(session, value), value, "Dark");

        write.DisconnectAll();
        await session.WaitForRenderedAsync(Token);
        AssertHollow(ShapeOf(session, write), write, "Dark");
        AssertHollow(ShapeOf(session, value), value, "Dark");

        Assert.True(graph.Connect(value, write));
        await session.WaitForRenderedAsync(Token);
        session.ClassContext.UndoRedo.Clear();
        graph.SelectNodes([graph.Nodes.Single(n => n.Node == literal)], deselectPrevious: true);
        await session.PressDeleteAsync(Token);
        await session.WaitForRenderedAsync(Token);
        AssertHollow(ShapeOf(session, write), write, "Dark");

        await session.PressUndoAsync(Token);
        await session.WaitForRenderedAsync(Token);
        AssertFilled(ShapeOf(session, write), write, "Dark");

        await session.PressRedoAsync(Token);
        await session.WaitForRenderedAsync(Token);
        AssertHollow(ShapeOf(session, write), write, "Dark");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NoPinKeepsTheSixtyPercentDimming()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        session.GraphViewModel.AddNode<IfElseNode>(new GraphPoint(28, 280));
        await session.WaitForRenderedAsync(Token);

        var shapes = session.Window.GetVisualDescendants().OfType<Shape>().Where(shape => shape.Classes.Contains("pin") && shape.IsEffectivelyVisible).ToList();

        Assert.NotEmpty(shapes);
        Assert.All(shapes, shape =>
        {
            Assert.Equal(1.0, shape.Opacity);
            Assert.DoesNotContain("dimmed", shape.Classes);
        });
    }
}
