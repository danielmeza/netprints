using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Material.Icons;
using Material.Icons.Avalonia;
using NetPrints.Editor.Icons;

namespace NetPrints.Editor.UITests.Icons;

/// <summary>FR-084: <see cref="IconPresenter"/> draws the registry's vector glyph at the icon sizes, in the inherited foreground.</summary>
public class IconPresenterTests
{
    private static (Window Window, IconPresenter Presenter, MaterialIcon Glyph) Show(string id, ThemeVariant variant, bool active = false, string? sizeClass = null)
    {
        var presenter = new IconPresenter { IconId = id, IsActive = active };
        if (sizeClass is not null)
        {
            presenter.Classes.Add(sizeClass);
        }

        var window = new Window { RequestedThemeVariant = variant, Content = presenter };
        window.Show();
        MaterialIcon glyph = Assert.Single(presenter.GetVisualDescendants().OfType<MaterialIcon>());
        return (window, presenter, glyph);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(null, 16.0)]
    [InlineData("medium", 20.0)]
    public void ItDrawsTheGlyphAtTheSmallAndMediumSizes(string? sizeClass, double size)
    {
        (Window window, IconPresenter presenter, MaterialIcon glyph) = Show(IconIds.Save, ThemeVariant.Dark, sizeClass: sizeClass);
        try
        {
            Assert.Equal(MaterialIconKind.ContentSave, glyph.Kind);
            Assert.Equal(size, presenter.Bounds.Width);
            Assert.Equal(size, presenter.Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheIconSizeTokensAreSixteenAndTwenty()
    {
        Assert.True(Application.Current is { } app && app.TryGetResource("Icon.Small", ThemeVariant.Default, out object? small) && Equals(small, 16.0));
        Assert.True(Application.Current is { } other && other.TryGetResource("Icon.Medium", ThemeVariant.Default, out object? medium) && Equals(medium, 20.0));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnActivePresenterDrawsTheFilledGlyphOfAPair()
    {
        (Window window, IconPresenter presenter, MaterialIcon glyph) = Show(IconIds.Project, ThemeVariant.Dark);
        try
        {
            Assert.Equal(MaterialIconKind.FolderOutline, glyph.Kind);
            presenter.IsActive = true;
            Assert.Equal(MaterialIconKind.Folder, glyph.Kind);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnIdWithoutAPairDrawsTheSameGlyphWhenActive()
    {
        (Window window, IconPresenter presenter, MaterialIcon glyph) = Show(IconIds.Save, ThemeVariant.Dark, active: true);
        try
        {
            Assert.Equal(MaterialIconKind.ContentSave, glyph.Kind);
            presenter.IsActive = false;
            Assert.Equal(MaterialIconKind.ContentSave, glyph.Kind);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void ItDrawsInTheInheritedForeground(string variantName)
    {
        ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var presenter = new IconPresenter { IconId = IconIds.Save };
        var host = new Border { Child = presenter };
        TextElement.SetForeground(host, Brushes.Red);
        var window = new Window { RequestedThemeVariant = variant, Content = host };
        window.Show();
        try
        {
            MaterialIcon glyph = Assert.Single(presenter.GetVisualDescendants().OfType<MaterialIcon>());
            Assert.Equal(Brushes.Red.Color, Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void ItFollowsTheThemeForegroundByDefault(string variantName)
    {
        ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        (Window window, _, MaterialIcon glyph) = Show(IconIds.Save, variant);
        try
        {
            IBrush? expected = TextElement.GetForeground(window);
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expected).Color, Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color);
        }
        finally
        {
            window.Close();
        }
    }
}
