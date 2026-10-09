using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Every pin colour reaches 3:1 (WCAG 2.x non-text contrast) against the node body and the canvas, a wire at the opacity it is drawn with (FR-107, SC-011, T092n).</summary>
public class PinContrastTests
{
    public const double MinimumContrast = 3.0;

    public static TheoryData<string, string> FamiliesByVariant()
    {
        TheoryData<string, string> data = [];
        foreach (string variant in new[] { "Dark", "Light" })
        {
            foreach (PinTypeFamily family in PinTypeFamily.All)
            {
                data.Add(variant, family.Name);
            }
        }

        return data;
    }

    public static Color Composite(Color top, Color bottom)
    {
        double alpha = top.A / 255.0;
        byte Mix(byte a, byte b) => (byte)Math.Round((a * alpha) + (b * (1 - alpha)));
        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    public static Color WithOpacity(Color color, double opacity) => Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B);

    public static double WireOpacity()
    {
        var theme = Assert.IsType<ControlTheme>(CanvasPaletteTests.Resolve("NetPrints.Connection", ThemeVariant.Default));
        return theme.Setters.OfType<Setter>()
            .Where(setter => setter.Property == Visual.OpacityProperty)
            .Select(setter => Assert.IsType<double>(setter.Value))
            .Single();
    }

    private static (Color Pin, Color Body, Color Canvas) Colours(string variant, string family)
    {
        ThemeVariant theme = CanvasPaletteTests.VariantOf(variant);
        Color canvas = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Canvas.Background", theme));
        Color card = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve("Node.CardBackground", theme));
        Color pin = CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve($"Pin.{family}", theme));
        return (pin, Composite(card, canvas), canvas);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(FamiliesByVariant))]
    public void APinReaches3To1AgainstTheNodeBodyAndTheCanvas(string variant, string family)
    {
        var (pin, body, canvas) = Colours(variant, family);

        Assert.True(CanvasPaletteTests.Contrast(pin, body) >= MinimumContrast, $"{family} pin on the body in {variant}: {CanvasPaletteTests.Contrast(pin, body):F2}");
        Assert.True(CanvasPaletteTests.Contrast(pin, canvas) >= MinimumContrast, $"{family} pin on the canvas in {variant}: {CanvasPaletteTests.Contrast(pin, canvas):F2}");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(FamiliesByVariant))]
    public void AWireReaches3To1AtItsOpacityOnTheCanvasAndOnTheNodeBody(string variant, string family)
    {
        var (pin, body, canvas) = Colours(variant, family);
        Color wire = WithOpacity(pin, WireOpacity());

        double onCanvas = CanvasPaletteTests.Contrast(Composite(wire, canvas), canvas);
        double onBody = CanvasPaletteTests.Contrast(Composite(wire, body), body);

        Assert.True(onCanvas >= MinimumContrast, $"{family} wire on the canvas in {variant}: {onCanvas:F2}");
        Assert.True(onBody >= MinimumContrast, $"{family} wire on the body in {variant}: {onBody:F2}");
    }
}
