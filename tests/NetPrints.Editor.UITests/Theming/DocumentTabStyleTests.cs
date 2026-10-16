using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Owner feedback 2026-10-15: the selected document tab flows into its document, like the active tab of an IDE, and the tabs are compact.</summary>
public class DocumentTabStyleTests
{
    private const string Active = "active";
    private const string Inactive = "inactive";
    private const string Focused = "focused";
    private const string Hover = "hover";
    private const double CenterTolerance = 1;
    private const double DockContentMargin = 2;
    private const double MaxStripHeight = 30;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string variant in new[] { "Dark", "Light" })
            {
                foreach (string state in new[] { Active, Inactive, Focused, Hover })
                {
                    data.Add(variant, state);
                }
            }

            return data;
        }
    }

    private static Border BodyOf(DocumentTabStripItem tab) =>
        tab.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_TabBody");

    private static T Named<T>(Visual root, string name)
        where T : Visual => root.GetVisualDescendants().OfType<T>().Single(visual => (visual as StyledElement)?.Name == name);

    private static object ResourceOf(Visual visual, string key, ThemeVariant variant)
    {
        Assert.True(visual.TryFindResource(key, variant, out object? value), key);
        return Assert.IsAssignableFrom<object>(value);
    }

    private static Color ColorOf(Visual visual, string key, ThemeVariant variant) => ResourceOf(visual, key, variant) switch
    {
        Color color => color,
        ISolidColorBrush brush => brush.Color,
        _ => throw new InvalidOperationException(key),
    };

    private static double NumberOf(Visual visual, string key, ThemeVariant variant) => Assert.IsType<double>(ResourceOf(visual, key, variant));

    private static Thickness ThicknessOf(Visual visual, string key, ThemeVariant variant) => Assert.IsType<Thickness>(ResourceOf(visual, key, variant));

    private static Rect InWindow(Visual visual, Visual window) =>
        new(visual.TranslatePoint(default, window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);

    private static uint Argb(Color color) => (uint)color.A << 24 | (uint)color.R << 16 | (uint)color.G << 8 | color.B;

    private static UiImage FrameOf(Window window)
    {
        HeadlessDriver.Pump();
        using WriteableBitmap frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
        using var stream = new MemoryStream();
        frame.Save(stream, new PngBitmapEncoderOptions());
        return new UiImage(stream.ToArray());
    }

    private static void SetUpState(Window window, DocumentControl document, DocumentTabStrip strip, string state)
    {
        var dock = Assert.IsAssignableFrom<IDock>(document.DataContext);
        dock.IsActive = state != Inactive && state != Hover;
        if (state == Focused)
        {
            InputElement canvas = document.GetVisualDescendants().OfType<InputElement>()
                .First(element => element.Focusable && element.IsEffectivelyVisible && element is not DocumentTabStripItem && element is not Button);
            Assert.True(canvas.Focus());
        }

        if (state == Hover)
        {
            window.MouseMove(strip.TranslatePoint(new Point(strip.Bounds.Width - 40, strip.Bounds.Height / 2), window) ?? throw new InvalidOperationException("The strip is not in the window."));
        }

        HeadlessDriver.Pump();
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Cases))]
    public async Task TheSelectedTabOpensIntoItsDocumentThroughAStripBorderThatSkipsIt(string variantName, string state)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        ThemeVariant previous = application.RequestedThemeVariant ?? ThemeVariant.Dark;
        application.RequestedThemeVariant = variant;
        try
        {
            await using var session = await EditorSession.OpenSampleMainAsync(Token);
            await session.PickMainFromTheTreeAsync(Token);
            await session.OpenClassGraphAsync(Token);
            await session.Page.OpenMethodAsync("Main", Token);
            await session.WaitForRenderedAsync(Token);

            Window window = session.Window;
            DocumentControl document = Assert.Single(window.GetVisualDescendants().OfType<DocumentControl>(), control => control.IsEffectivelyVisible);
            DocumentTabStrip strip = document.GetVisualDescendants().OfType<DocumentTabStrip>().Single();
            SetUpState(window, document, strip, state);
            Assert.Equal(state is Active or Focused, document.Classes.Contains(":active"));

            List<DocumentTabStripItem> tabs = [.. window.GetVisualDescendants().OfType<DocumentTabStripItem>()];
            Assert.Equal(3, tabs.Count);
            DocumentTabStripItem selected = Assert.Single(tabs, tab => tab.IsSelected);
            Border body = BodyOf(selected);

            Border gutter = Assert.IsType<Border>(body.Parent);
            Assert.Equal("PART_TabGutter", gutter.Name);
            Assert.Equal(0, gutter.CornerRadius.BottomLeft);
            Assert.Equal(0, gutter.CornerRadius.BottomRight);
            Assert.True(gutter.CornerRadius.TopLeft > 0);
            Assert.Equal(0, body.BorderThickness.Bottom);
            Assert.Equal(1, body.Opacity);

            Color surface = Assert.IsAssignableFrom<ISolidColorBrush>(body.Background).Color;
            Assert.Equal(ColorOf(window, "Canvas.Background", variant), surface);
            ItemsControl breadcrumbs = window.GetVisualDescendants().OfType<ItemsControl>()
                .Single(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == AutomationIds.Breadcrumbs);
            Assert.Equal(surface, Assert.IsAssignableFrom<ISolidColorBrush>(breadcrumbs.Background).Color);

            Color stripBorder = ColorOf(window, "Tab.StripBorder", variant);
            Assert.Equal(stripBorder, Assert.IsAssignableFrom<ISolidColorBrush>(body.BorderBrush).Color);
            Assert.Equal(1, body.BorderThickness.Left);
            Assert.Equal(1, body.BorderThickness.Right);
            Assert.Equal(0, body.BorderThickness.Top);
            Assert.Equal(ColorOf(window, "Tab.SelectedAccent", variant), Assert.IsAssignableFrom<ISolidColorBrush>(gutter.BorderBrush).Color);
            Assert.Equal(2, gutter.BorderThickness.Top);
            Assert.Equal(0, gutter.BorderThickness.Left);

            foreach (DocumentTabStripItem inactive in tabs.Where(tab => !tab.IsSelected))
            {
                Assert.True(BodyOf(inactive).Opacity < 1);
            }

            Panel line = Named<Panel>(document, "PART_DocumentSeperator");
            Panel host = Named<Panel>(document, "PART_DocumentSeperatorHost");
            Assert.True(line.IsEffectivelyVisible);
            Assert.Equal(stripBorder, Assert.IsAssignableFrom<ISolidColorBrush>(line.Background).Color);
            Rect lineBox = InWindow(line, window);
            Assert.Equal(1, lineBox.Height);
            Assert.Equal(1, host.Bounds.Height);
            Assert.True(lineBox.Width >= strip.Bounds.Width);

            Assert.True(InWindow(body, window).Bottom > lineBox.Top, "the selected tab covers the strip border");
            Assert.All(tabs.Where(tab => !tab.IsSelected), tab => Assert.True(InWindow(BodyOf(tab), window).Bottom <= lineBox.Top, "an inactive tab leaves the strip border visible"));

            Border frame = Named<Border>(document, "PART_Border");
            Assert.Equal(new Thickness(0), frame.BorderThickness);

            AssertLineIsDrawn(window, tabs, lineBox, stripBorder, surface);
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }

    private static void AssertLineIsDrawn(Window window, List<DocumentTabStripItem> tabs, Rect lineBox, Color stripBorder, Color surface)
    {
        UiImage frame = FrameOf(window);
        int y = (int)lineBox.Top;
        uint expectedLine = Argb(stripBorder);
        uint expectedSurface = Argb(surface);

        DocumentTabStripItem selected = tabs.Single(tab => tab.IsSelected);
        int underSelected = (int)InWindow(selected, window).Left + 3;
        Assert.Equal(expectedSurface, frame.Pixel(underSelected, y));

        foreach (DocumentTabStripItem inactive in tabs.Where(tab => !tab.IsSelected))
        {
            int x = (int)InWindow(inactive, window).Left + 3;
            Assert.Equal(expectedLine, frame.Pixel(x, y));
            Assert.NotEqual(expectedLine, frame.Pixel(x, y - 1));
            Assert.Equal(expectedSurface, frame.Pixel(x, y + 1));
        }

        int beyond = (int)InWindow(tabs[^1], window).Right + 60;
        Assert.Equal(expectedLine, frame.Pixel(beyond, y));
        Assert.NotEqual(expectedLine, frame.Pixel(beyond, y - 1));
        Assert.Equal(expectedSurface, frame.Pixel(beyond, y + 1));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task TheTabsAreCompactAndEveryLabelSharesOneBaseline(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        ThemeVariant previous = application.RequestedThemeVariant ?? ThemeVariant.Dark;
        application.RequestedThemeVariant = variant;
        try
        {
            await using var session = await EditorSession.OpenSampleMainAsync(Token);
            await session.PickMainFromTheTreeAsync(Token);
            await session.OpenClassGraphAsync(Token);
            await session.Page.OpenMethodAsync("Main", Token);
            await session.WaitForRenderedAsync(Token);

            Window window = session.Window;
            List<DocumentTabStripItem> tabs = [.. window.GetVisualDescendants().OfType<DocumentTabStripItem>()];
            Assert.Equal(3, tabs.Count);

            double buttonSize = NumberOf(window, "Tab.CloseButtonSize", variant);
            double iconSize = NumberOf(window, "DockChromeIconSize", variant);
            Thickness padding = ThicknessOf(window, "Tab.BodyPadding", variant);
            Thickness border = ThicknessOf(window, "Tab.SelectedBorderThickness", variant);
            Assert.Equal(20, buttonSize);
            Assert.Equal(16, iconSize);

            DocumentTabStrip strip = window.GetVisualDescendants().OfType<DocumentTabStrip>().Single(candidate => candidate.IsEffectivelyVisible);
            double tabHeight = NumberOf(window, "Tab.Height", variant);
            Assert.InRange(tabHeight, 1, MaxStripHeight);
            Assert.Equal(tabHeight, strip.Bounds.Height);

            var labelTops = new List<double>();
            var labelHeights = new List<double>();
            var labelLefts = new List<double>();
            foreach (DocumentTabStripItem tab in tabs)
            {
                TextBlock label = Named<ContentPresenter>(tab, "PART_HeaderPresenter").GetVisualDescendants().OfType<TextBlock>().Single();
                ContentPresenter closeHost = Named<ContentPresenter>(tab, "PART_ClosePresenter");
                Button close = closeHost.GetVisualDescendants().OfType<Button>().Single();
                Avalonia.Controls.Shapes.Path glyph = Named<Avalonia.Controls.Shapes.Path>(tab, "PART_ClosePath");
                Rect tabBox = InWindow(tab, window);
                Rect labelBox = InWindow(label, window);
                Rect closeBox = InWindow(close, window);

                Assert.True(close.IsEffectivelyVisible, "every tab shows its close button");
                Assert.Equal(buttonSize, closeBox.Width);
                Assert.Equal(buttonSize, closeBox.Height);
                Assert.Equal(iconSize, glyph.Bounds.Width);
                Assert.Equal(iconSize, glyph.Bounds.Height);
                Assert.InRange(closeBox.Left - labelBox.Right, 4, 8);
                Assert.Equal(padding.Left + border.Left + DockContentMargin, labelBox.Left - tabBox.Left);
                Assert.Equal(padding.Right + border.Right + DockContentMargin, tabBox.Right - closeBox.Right);
                Assert.Equal(NumberOf(window, "Tab.Height", variant), tabBox.Height);
                Assert.InRange(Math.Abs(tabBox.Center.Y - closeBox.Center.Y), 0, CenterTolerance);
                Assert.InRange(Math.Abs(tabBox.Center.Y - labelBox.Center.Y), 0, CenterTolerance);

                labelTops.Add(labelBox.Top);
                labelHeights.Add(labelBox.Height);
                labelLefts.Add(labelBox.Left - tabBox.Left);
            }

            Assert.Single(labelTops.Distinct());
            Assert.Single(labelHeights.Distinct());
            Assert.Single(labelLefts.Distinct());
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }
}
