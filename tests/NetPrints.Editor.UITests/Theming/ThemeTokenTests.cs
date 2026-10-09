using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>The visual-system tokens of FR-080 (research R11): type ramp, spacing scale, inspector column and colours in both variants.</summary>
public class ThemeTokenTests
{
    private static readonly string[] ColourTokens =
    [
        "GraphGrid.MinorColor",
        "GraphGrid.MajorColor",
        "GraphWatermark.Foreground",
        "Node.CardBackground",
        "Node.CardShadow",
        "Node.TitleForeground",
        "Node.SubtitleForeground",
        "OpeningGraphIndicator.Background",
        "OpeningGraphIndicator.Foreground",
        "BusyOverlay.Background",
        "Popup.Shadow",
        "Pin.HoverStroke",
        "Diagnostic.Error.Foreground",
        "Diagnostic.Warning.Foreground",
        "Diagnostic.Info.Foreground",
        "CommandBar.BadgeBackground",
        "CommandBar.BadgeForeground",
        "Icon.Method.Foreground",
        "Icon.Property.Foreground",
        "Icon.Event.Foreground",
        "Icon.Class.Foreground",
        "Node.Border",
        "Node.HeaderForeground",
        "Node.Header.Entry",
        "Node.Header.Call",
        "Node.Header.Pure",
        "Node.Header.Flow",
        "Node.Header.Variable",
        "Node.Header.Constructor",
        "Node.Header.Async",
        "Node.Header.Throw",
        "Pin.Exec",
        "Pin.Data",
        "Pin.Type",
        "Pin.Bool",
        "Pin.Integer",
        "Pin.Float",
        "Pin.String",
        "Pin.Object",
        "Pin.ValueType",
        "Pin.Delegate",
        "Pin.Generic",
        "Pin.DefaultValue",
        "Pin.Hint",
        "Canvas.SelectionBorder",
        "Canvas.MarqueeFill",
        "Canvas.MarqueeBorder",
        "Canvas.WireSelected",
    ];

    private static readonly string[] CanvasThemes =
    [
        "NetPrints.NodifyEditor",
        "NetPrints.SelectionRectangle",
        "NetPrints.ItemContainer",
        "NetPrints.Node",
        "NetPrints.Connection",
        "NetPrints.Connector",
    ];

    private static object? Resolve(string key, ThemeVariant variant) =>
        Application.Current is { } app && app.TryGetResource(key, variant, out object? value) ? value : null;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Font.Caption", 12.0)]
    [InlineData("Font.Body", 14.0)]
    [InlineData("Font.Subtitle", 16.0)]
    [InlineData("Font.Title", 20.0)]
    [InlineData("Font.Watermark", 32.0)]
    public void TheTypeRampHasTheFontSizes(string key, double size)
    {
        Assert.Equal(size, Assert.IsType<double>(Resolve(key, ThemeVariant.Dark)));
        Assert.Equal(size, Assert.IsType<double>(Resolve(key, ThemeVariant.Light)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void FontMonoIsCascadiaMono()
    {
        FontFamily mono = Assert.IsType<FontFamily>(Resolve("Font.Mono", ThemeVariant.Default));
        Assert.Equal(new FontFamily("avares://NetPrints.Editor/Assets/Fonts#Cascadia Mono"), mono);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("XS", 4.0)]
    [InlineData("S", 8.0)]
    [InlineData("M", 12.0)]
    [InlineData("L", 16.0)]
    [InlineData("XL", 24.0)]
    public void TheSpacingScaleIsAvailableAsThicknessAndDouble(string step, double size)
    {
        Assert.Equal(new Thickness(size), Assert.IsType<Thickness>(Resolve($"Space.{step}", ThemeVariant.Default)));
        Assert.Equal(size, Assert.IsType<double>(Resolve($"Space.{step}.Size", ThemeVariant.Default)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheInspectorLabelColumnHasOneWidth()
    {
        GridLength width = Assert.IsType<GridLength>(Resolve("Inspector.LabelColumnWidth", ThemeVariant.Default));
        Assert.True(width.IsAbsolute);
        Assert.True(width.Value > 0);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(ColourTokenKeys))]
    public void EveryColourTokenResolvesInDarkAndLight(string key)
    {
        Assert.NotNull(Resolve(key, ThemeVariant.Dark));
        Assert.NotNull(Resolve(key, ThemeVariant.Light));
    }

    public static TheoryData<string> ColourTokenKeys() => [.. ColourTokens];

    public static TheoryData<string> CanvasThemeKeys() => [.. CanvasThemes];

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasThemeKeys))]
    public void EveryCanvasControlThemeIsAvailable(string key)
    {
        Assert.IsType<ControlTheme>(Resolve(key, ThemeVariant.Default));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePanelHeaderIsLeftAlignedSemiboldAtSubtitleSize()
    {
        var header = new TextBlock { Classes = { "header" } };
        var window = new Window { Content = header };
        window.Show();
        try
        {
            Assert.Equal(HorizontalAlignment.Left, header.HorizontalAlignment);
            Assert.Equal(FontWeight.SemiBold, header.FontWeight);
            Assert.Equal(16.0, header.FontSize);
        }
        finally
        {
            window.Close();
        }
    }
}
