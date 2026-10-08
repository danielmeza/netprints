using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Editor.UITests.Theming;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>The "self" hint on an unconnected Target pin, as the pin row draws it (FR-096, T091b).</summary>
public class TargetPinSelfViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Color Composite(Color top, Color bottom)
    {
        double alpha = top.A / 255.0;
        byte Mix(byte a, byte b) => (byte)Math.Round((a * alpha) + (b * (1 - alpha)));
        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public async Task TheTargetPinRowShowsSelfWithAHintBrushThatReadsOnTheNodeBody(string variant)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.AddVariableAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        method.Modifiers = MethodModifiers.None;
        var variable = session.ClassContext.Variables.Single().Variable.Specifier;
        var getter = session.GraphViewModel.AddNode<VariableGetterNode>(new GraphPoint(28, 280), null, variable);
        await session.WaitForRenderedAsync(Token);
        CanvasPaletteTests.App.RequestedThemeVariant = CanvasPaletteTests.VariantOf(variant);
        try
        {
            NodeView view = session.Window.GetVisualDescendants().OfType<NodeView>().Single(v => CanvasPaletteTests.ModelOf(v).Node == getter);
            var hints = view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("pinHint") && t.IsEffectivelyVisible).ToList();

            TextBlock hint = Assert.Single(hints);
            Assert.Equal("self", hint.Text);

            Color hintColour = CanvasPaletteTests.ColorOf(hint.Foreground);
            Assert.Equal(CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Pin.Hint", CanvasPaletteTests.VariantOf(variant))), hintColour);

            Color card = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Node.CardBackground", CanvasPaletteTests.VariantOf(variant)));
            Color region = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("SystemRegionColor", CanvasPaletteTests.VariantOf(variant)));
            Assert.True(CanvasPaletteTests.Contrast(hintColour, Composite(card, region)) >= CanvasPaletteTests.MinimumContrast);
        }
        finally
        {
            CanvasPaletteTests.App.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AStaticGraphShowsNoHint()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.AddVariableAsync(Token);
        var variable = session.ClassContext.Variables.Single().Variable.Specifier;
        session.GraphViewModel.AddNode<VariableGetterNode>(new GraphPoint(28, 280), null, variable);
        await session.WaitForRenderedAsync(Token);

        Assert.DoesNotContain(session.Window.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("pinHint") && t.IsEffectivelyVisible);
    }
}
