using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Owner feedback 2026-10-15: the selected document tab joins its document instead of looking like a button.</summary>
public class DocumentTabStyleTests
{
    private static readonly DocumentId First = DocumentId.Graph("A.cs", "method:1");
    private static readonly DocumentId Second = DocumentId.Graph("A.cs", "method:2");
    private static readonly DocumentId Third = DocumentId.Graph("A.cs", "method:3");

    private static Border BodyOf(DocumentTabStripItem tab) =>
        tab.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_TabBody");

    private static Color TokenColor(Visual visual, string key, ThemeVariant variant)
    {
        Assert.True(visual.TryFindResource(key, variant, out object? value));
        return value switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException(key),
        };
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TheSelectedTabIsTheDocumentSurfaceWithSquareBottomCornersAndNothingUnderIt(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        ThemeVariant previous = application.RequestedThemeVariant ?? ThemeVariant.Dark;
        application.RequestedThemeVariant = variant;
        try
        {
            using var rig = ShellRig.Create();
            rig.Api.OpenDocument(First);
            rig.Api.OpenDocument(Second);
            rig.Api.OpenDocument(Third);
            rig.Api.ActivateDocument(Second);
            rig.Settle();

            List<DocumentTabStripItem> tabs = [.. rig.Main.GetVisualDescendants().OfType<DocumentTabStripItem>()];
            Assert.Equal(3, tabs.Count);
            DocumentTabStripItem selected = Assert.Single(tabs, tab => tab.IsSelected);
            Border body = BodyOf(selected);

            Assert.Equal(0, body.CornerRadius.BottomLeft);
            Assert.Equal(0, body.CornerRadius.BottomRight);
            Assert.True(body.CornerRadius.TopLeft > 0);
            Assert.Equal(TokenColor(rig.Main, "Canvas.Background", variant), Assert.IsAssignableFrom<ISolidColorBrush>(body.Background).Color);
            Assert.Equal(TokenColor(rig.Main, "Tab.SelectedAccent", variant), Assert.IsAssignableFrom<ISolidColorBrush>(body.BorderBrush).Color);
            Assert.Equal(0, body.BorderThickness.Bottom);
            Assert.Equal(1, body.Opacity);

            foreach (DocumentTabStripItem inactive in tabs.Where(tab => !tab.IsSelected))
            {
                Assert.True(BodyOf(inactive).Opacity < 1);
            }

            DocumentControl document = Assert.Single(rig.Main.GetVisualDescendants().OfType<DocumentControl>());
            Assert.All(document.GetVisualDescendants().OfType<Panel>().Where(panel => panel.Name == "PART_DocumentSeperator"), panel => Assert.False(panel.IsVisible));
            Border frame = document.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Border");
            Assert.Equal(new Thickness(0), frame.BorderThickness);
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }
}
