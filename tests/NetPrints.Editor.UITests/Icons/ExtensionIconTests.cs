using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.UITests.Icons;

/// <summary>N8, ADR-0021: an extension icon id <c>&lt;extension id&gt;.&lt;name&gt;</c> resolves through <c>Icon.&lt;id&gt;</c> in a dictionary added with <c>AddViewResources</c>.</summary>
public class ExtensionIconTests
{
    private const string ExtensionIconId = "fx.editor.hello";
    private const string ResourceKey = "Icon.fx.editor.hello";

    private static ResourceDictionary Dictionary(Geometry geometry) => new() { [ResourceKey] = geometry };

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnExtensionIdResolvesToTheGeometryOfItsResourceKey()
    {
        var geometry = StreamGeometry.Parse("M0,0 L10,10");
        Application application = Application.Current ?? throw new InvalidOperationException("No application.");
        ResourceDictionary dictionary = Dictionary(geometry);
        application.Resources.MergedDictionaries.Add(dictionary);
        try
        {
            var registry = IconRegistry.ForApplication(application, NullLogger<IconRegistry>.Instance);

            IconGlyph glyph = registry.Resolve(ExtensionIconId);

            Assert.Same(geometry, glyph.Geometry);
            Assert.True(registry.IsKnown(ExtensionIconId));
            Assert.Empty(registry.Issues);
        }
        finally
        {
            application.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnUnknownExtensionIdDrawsTheFallbackAndRecordsNpx010Once()
    {
        Application application = Application.Current ?? throw new InvalidOperationException("No application.");
        var registry = IconRegistry.ForApplication(application, NullLogger<IconRegistry>.Instance);

        IconGlyph first = registry.Resolve("fx.missing.icon");
        IconGlyph second = registry.Resolve("fx.missing.icon");

        Assert.Equal(registry.Resolve(IconIds.Unknown), first);
        Assert.Equal(first, second);
        Assert.Null(first.Geometry);
        IconIssue issue = Assert.Single(registry.Issues);
        Assert.Equal(ExtensionDiagnosticCodes.UnknownReference, issue.Code);
        Assert.Equal("NPX010", issue.Code);
        Assert.Equal("fx.missing.icon", issue.IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePresenterDrawsTheExtensionGeometryInsteadOfAMaterialGlyph()
    {
        var geometry = StreamGeometry.Parse("M0,0 L10,10");
        Application application = Application.Current ?? throw new InvalidOperationException("No application.");
        ResourceDictionary dictionary = Dictionary(geometry);
        application.Resources.MergedDictionaries.Add(dictionary);
        IconRegistry previous = IconRegistry.Shared;
        IconRegistry.Shared = IconRegistry.ForApplication(application, NullLogger<IconRegistry>.Instance);
        var presenter = new IconPresenter { IconId = ExtensionIconId };
        var window = new Window { RequestedThemeVariant = ThemeVariant.Dark, Content = presenter };
        window.Show();
        try
        {
            PathIcon path = Assert.Single(presenter.GetVisualDescendants().OfType<PathIcon>());
            Assert.Same(geometry, path.Data);
            Assert.True(path.IsVisible);
            Assert.All(presenter.GetVisualDescendants().OfType<Material.Icons.Avalonia.MaterialIcon>(), icon => Assert.False(icon.IsVisible));
        }
        finally
        {
            window.Close();
            IconRegistry.Shared = previous;
            application.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
