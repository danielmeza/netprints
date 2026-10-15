using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using NetPrints.Editor.UITests.Hosting;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The Dock chrome glyphs share one grid, one box and one button size, whatever their own geometry.</summary>
public class DockChromeIconTests
{
    private const double GlyphBox = 16;
    private const double ButtonSize = 24;
    private const double Grid = 24;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ToolChromeGlyphsAreTheSameSize()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(TestContext.Current.CancellationToken);
        var chromes = session.Window.GetVisualDescendants().OfType<ToolChromeControl>().ToList();
        Assert.NotEmpty(chromes);

        var seen = new HashSet<string>();
        foreach (var chrome in chromes)
        {
            foreach (var button in chrome.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "PART_MenuButton" or "PART_PinButton" or "PART_CloseButton"))
            {
                seen.Add(Assert.IsType<string>(button.Name));
                var viewbox = button.GetVisualDescendants().OfType<Viewbox>().First();
                var path = viewbox.GetVisualDescendants().OfType<ShapePath>().First();
                Assert.True(button.Bounds.Width >= ButtonSize && button.Bounds.Height >= ButtonSize, $"{button.Name} hit target {button.Bounds.Size}");
                Assert.Equal(ButtonSize, button.Bounds.Height);
                Assert.Equal(GlyphBox, viewbox.Bounds.Width, 0.5);
                Assert.Equal(GlyphBox, viewbox.Bounds.Height, 0.5);
                var data = Assert.IsAssignableFrom<Geometry>(path.Data);
                Assert.Equal(Grid, data.Bounds.Width, 0.01);
                Assert.Equal(Grid, data.Bounds.Height, 0.01);
            }
        }

        Assert.Equal(["PART_CloseButton", "PART_MenuButton", "PART_PinButton"], seen.Order().ToList());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DocumentTabCloseUsesTheSameBoxAndGrid()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(TestContext.Current.CancellationToken);
        var item = session.Window.GetVisualDescendants().OfType<DocumentTabStripItem>().First();
        var path = item.GetVisualDescendants().OfType<ShapePath>().First(p => p.Bounds.Width > 0);
        var button = path.GetVisualAncestors().OfType<Button>().First();
        Assert.True(button.Bounds.Width >= ButtonSize, $"tab close hit target {button.Bounds.Size}");
        Assert.Equal(GlyphBox, path.Bounds.Width, 0.5);
        Assert.Equal(GlyphBox, path.Bounds.Height, 0.5);
        Assert.Equal(Grid, Assert.IsAssignableFrom<Geometry>(path.Data).Bounds.Width, 0.01);
    }
}
