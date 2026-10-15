using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Icons;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using Nodify.Avalonia;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Node-header roles, kind glyphs, pin and selection tokens, and the canvas theming of FR-086 (T091, T091a).</summary>
public class CanvasPaletteTests
{
    public const double MinimumContrast = 4.5;

    private static readonly string[] PinTokens =
    [
        "Pin.Exec", "Pin.Type", "Pin.Bool", "Pin.Integer", "Pin.Float", "Pin.String", "Pin.Object", "Pin.ValueType", "Pin.Delegate", "Pin.Generic",
    ];

    private static readonly string[] CanvasTokens = ["Canvas.SelectionBorder", "Canvas.MarqueeFill", "Canvas.MarqueeBorder", "Canvas.WireSelected"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Application App => Application.Current ?? throw new InvalidOperationException("No application.");

    public static TheoryData<string> Variants() => ["Dark", "Light"];

    public static NodeViewModel ModelOf(NodeView view) => view.DataContext as NodeViewModel ?? throw new InvalidOperationException("A node view without its view model.");

    public static TheoryData<string, string> RolesByVariant()
    {
        TheoryData<string, string> data = [];
        foreach (string variant in new[] { "Dark", "Light" })
        {
            foreach (NodeRole role in NodeRole.All)
            {
                data.Add(variant, role.Name);
            }
        }

        return data;
    }

    public static TheoryData<string, string> TokensByVariant()
    {
        TheoryData<string, string> data = [];
        foreach (string variant in new[] { "Dark", "Light" })
        {
            foreach (string token in PinTokens.Concat(CanvasTokens).Append("Node.Border").Append("Node.HeaderForeground"))
            {
                data.Add(variant, token);
            }
        }

        return data;
    }

    public static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    public static double Contrast(Color a, Color b)
    {
        double x = Luminance(a);
        double y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    public static ThemeVariant VariantOf(string name) => name == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

    public static Color ColorOf(object? resource) => resource switch
    {
        ISolidColorBrush brush => brush.Color,
        Color color => color,
        _ => throw new InvalidOperationException($"Not a colour resource: {resource}"),
    };

    public static object? Resolve(string key, ThemeVariant variant) =>
        Application.Current is { } app && app.TryGetResource(key, variant, out object? value) ? value : null;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(RolesByVariant))]
    public void EveryRoleHeaderReachesTheContrastOfTheHeaderText(string variant, string role)
    {
        Color header = ColorOf(Resolve($"Node.Header.{role}", VariantOf(variant)));
        Color text = ColorOf(Resolve("Node.HeaderForeground", VariantOf(variant)));

        Assert.True(Contrast(header, text) >= MinimumContrast, $"{role} in {variant}: {Contrast(header, text):F2}");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(TokensByVariant))]
    public void ThePinAndCanvasTokensResolveInBothVariants(string variant, string token)
    {
        Assert.NotNull(Resolve(token, VariantOf(variant)));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Variants))]
    public async Task EachHeaderShowsItsRoleColourAndKindGlyphInBothVariants(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        App.RequestedThemeVariant = VariantOf(variant);
        try
        {
            var views = session.Window.GetVisualDescendants().OfType<NodeView>().ToList();
            Assert.NotEmpty(views);

            foreach (NodeView view in views)
            {
                var node = Assert.IsType<NodeViewModel>(view.DataContext);
                Border header = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Header");
                IconPresenter glyph = header.GetVisualDescendants().OfType<IconPresenter>().First(icon => icon.IconId == NodeIcons.For(node.VisualKind));

                Assert.Contains(node.Role.StyleClass, header.Classes);
                Assert.Equal(NodeIcons.For(node.VisualKind), glyph.IconId);
                Assert.Equal(ColorOf(Resolve($"Node.Header.{node.Role.Name}", VariantOf(variant))), ColorOf(header.Background));
            }
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheSampleGraphHeadersCarryTheirRoleClass()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        var roles = session.Window.GetVisualDescendants().OfType<NodeView>()
            .ToDictionary(v => ModelOf(v).Node.Name, v => v.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Header").Classes.Contains("role-entry"));

        Assert.True(roles["MethodEntryNode"]);
        Assert.True(roles["ReturnNode"]);
        Assert.False(roles["CallMethodNode"]);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Variants))]
    public async Task EveryPinAndWireDrawsTheTokenOfItsFamily(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.GraphViewModel;
        graph.AddNode<IfElseNode>(new GraphPoint(28, 280));
        graph.AddNode<TypeNode>(new GraphPoint(28, 420), null, TypeSpecifier.FromType<string>());
        var literal = Assert.IsType<LiteralNode>(graph.AddNode<LiteralNode>(new GraphPoint(300, 420), null, TypeSpecifier.FromType<string>()));
        var write = graph.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        Assert.True(graph.Connect(graph.Nodes.SelectMany(n => n.AllPins).Single(p => p.Pin == literal.ValuePin), write));
        await session.WaitForRenderedAsync(Token);
        App.RequestedThemeVariant = VariantOf(variant);
        try
        {
            var shapes = session.Window.GetVisualDescendants().OfType<Shape>().Where(s => s.Classes.Contains("pin") && s.IsEffectivelyVisible).ToList();
            var families = shapes.Select(shape => Assert.IsType<NodePinViewModel>(shape.DataContext).Family).Distinct().ToList();
            Assert.Contains(PinTypeFamily.Exec, families);
            Assert.Contains(PinTypeFamily.Type, families);
            Assert.Contains(PinTypeFamily.String, families);
            Assert.Contains(PinTypeFamily.Bool, families);

            foreach (Shape shape in shapes)
            {
                var pin = Assert.IsType<NodePinViewModel>(shape.DataContext);
                Color token = ColorOf(Resolve(pin.Family.TokenKey, VariantOf(variant)));
                Assert.True(shape.Stroke is not null, $"{shape.GetType().Name} {string.Join(' ', shape.Classes)}");
                Assert.Equal(token, ColorOf(shape.Stroke));
                if (pin.IsConnected)
                {
                    Assert.Equal(token, ColorOf(shape.Fill));
                }
            }

            var cables = session.Window.GetVisualDescendants().OfType<Nodify.Avalonia.Connections.Connection>()
                .Where(c => AutomationProperties.GetAutomationId(c) == AutomationIds.Connection).ToList();
            Assert.Contains(cables, c => Assert.IsType<ConnectionViewModel>(c.DataContext).Source.Family == PinTypeFamily.String);
            Assert.Contains(cables, c => Assert.IsType<ConnectionViewModel>(c.DataContext).Source.Family == PinTypeFamily.Exec);
            foreach (var cable in cables)
            {
                var family = Assert.IsType<ConnectionViewModel>(cable.DataContext).Source.Family;
                Assert.Equal(ColorOf(Resolve(family.TokenKey, VariantOf(variant))), ColorOf(cable.Stroke));
            }

            var selected = cables[0];
            ((IPseudoClasses)selected.Classes).Set(":selected", true);
            Assert.Equal(ColorOf(Resolve("Canvas.WireSelected", VariantOf(variant))), ColorOf(selected.Stroke));
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ThePreviewCableTakesTheFamilyOfThePinItStartsFrom()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var call = session.Graph.Node("CallMethodNode");

        await session.Driver.PressAndMoveAsync(await call.Input("value").Connector.CenterAsync(Token), await session.Graph.OffsetAsync(700, 450, Token), UiButton.Left, Token);
        var pending = session.Window.GetVisualDescendants().OfType<Nodify.Avalonia.Connections.PendingConnection>().Single();
        Assert.Equal(ColorOf(Resolve("Pin.String", ThemeVariant.Dark)), ColorOf(pending.Stroke));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Variants))]
    public async Task ASelectedNodeUsesTheSelectionBorderToken(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        App.RequestedThemeVariant = VariantOf(variant);
        try
        {
            NodeViewModel node = session.GraphViewModel.Nodes.First(n => !n.IsRerouteNode);
            NodeView view = session.Window.GetVisualDescendants().OfType<NodeView>().Single(v => v.DataContext == node);
            Border border = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "NodeBorder");

            node.IsSelected = false;
            Assert.Equal(ColorOf(Resolve("Node.Border", VariantOf(variant))), ColorOf(border.BorderBrush));

            node.IsSelected = true;
            Assert.Equal(ColorOf(Resolve("Canvas.SelectionBorder", VariantOf(variant))), ColorOf(border.BorderBrush));
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Variants))]
    public async Task TheMarqueeUsesTheMarqueeTokens(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        App.RequestedThemeVariant = VariantOf(variant);
        try
        {
            NodifyEditor editor = session.Window.GetVisualDescendants().OfType<NodifyEditor>().Single();
            var marquee = new Border { Theme = editor.SelectionRectangleTheme };
            var host = new Window { Content = marquee, RequestedThemeVariant = VariantOf(variant) };
            host.Show();
            try
            {
                Assert.Equal(ColorOf(Resolve("Canvas.MarqueeFill", VariantOf(variant))), ColorOf(marquee.Background));
                Assert.Equal(ColorOf(Resolve("Canvas.MarqueeBorder", VariantOf(variant))), ColorOf(marquee.BorderBrush));
            }
            finally
            {
                host.Close();
            }
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheNodeColourConvertersAreGone()
    {
        string[] gone = ["NodeKindBrushConverter", "PinKindBrushConverter", "PinFillConverter", "SelectedBorderConverter", "DefaultValueBrushConverter", "GraphBrushes"];
        var editorTypes = typeof(NodeView).Assembly.GetTypes().Select(t => t.Name).ToHashSet();

        Assert.All(gone, name => Assert.DoesNotContain(name, editorTypes));
    }
}
