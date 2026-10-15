using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Owner feedback 2026-10-15: the selected document tab flows into its document, like the active tab of an IDE.</summary>
public class DocumentTabStyleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Border BodyOf(DocumentTabStripItem tab) =>
        tab.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_TabBody");

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

    private static Rect InWindow(Visual visual, Visual window) =>
        new(visual.TranslatePoint(default, window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task TheSelectedTabOpensIntoItsDocumentThroughAStripBorderThatSkipsIt(string variantName)
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

            DocumentControl document = Assert.Single(window.GetVisualDescendants().OfType<DocumentControl>(), control => control.IsEffectivelyVisible);
            Panel line = document.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "PART_DocumentSeperator");
            Assert.True(line.IsEffectivelyVisible);
            Assert.Equal(stripBorder, Assert.IsAssignableFrom<ISolidColorBrush>(line.Background).Color);
            Rect lineBox = InWindow(line, window);
            Assert.Equal(1, lineBox.Height);
            DocumentTabStrip strip = document.GetVisualDescendants().OfType<DocumentTabStrip>().Single();
            Assert.True(lineBox.Width >= strip.Bounds.Width);

            Assert.True(InWindow(body, window).Bottom > lineBox.Top, "the selected tab covers the strip border");
            Assert.All(tabs.Where(tab => !tab.IsSelected), tab => Assert.True(InWindow(BodyOf(tab), window).Bottom <= lineBox.Top, "an inactive tab leaves the strip border visible"));

            Border frame = document.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Border");
            Assert.Equal(new Thickness(0), frame.BorderThickness);
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }
}
