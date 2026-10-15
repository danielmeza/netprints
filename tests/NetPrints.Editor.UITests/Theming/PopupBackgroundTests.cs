using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NetPrints.Editor.Graph.GetSet;
using NetPrints.Editor.Search;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>The canvas popups (node search, Get/Set chooser) paint a surface colour, not the pure black of the dark system key.</summary>
public class PopupBackgroundTests
{
    private static readonly Color Black = Color.FromRgb(0, 0, 0);

    private static Color BackgroundOf(Control view, ThemeVariant variant)
    {
        var window = new Window { Content = view, RequestedThemeVariant = variant };
        window.Show();
        try
        {
            Driving.HeadlessDriver.Pump();
            Border root = view.GetVisualDescendants().OfType<Border>().First();
            return Assert.IsAssignableFrom<ISolidColorBrush>(root.Background).Color;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheNodeSearchIsNotBlackInDark()
    {
        Assert.NotEqual(Black, BackgroundOf(new NodeSearchView(), ThemeVariant.Dark));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheGetSetChooserIsNotBlackInDark()
    {
        Assert.NotEqual(Black, BackgroundOf(new GetSetChooserView(), ThemeVariant.Dark));
    }
}
