using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using NetPrints.Editor.Theming;
using NetPrints.Testing;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>FR-066: the token catalogue covers every visual token of the theme dictionaries and each token resolves to the kind the catalogue names.</summary>
public class ThemeCatalogueTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex StaticResourcePattern = new(@"\{StaticResource\s+(?:ResourceKey=)?(?<key>[\w.]+)\}|<StaticResource\b[^>]*?\bResourceKey=""(?<key>[\w.]+)""");

    private static readonly string[] ValueElements = ["Double", "Thickness", "CornerRadius", "GridLength", "Color", "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "ConicGradientBrush"];

    internal static IReadOnlyList<string> StaticResourceKeys(string xaml) => [.. StaticResourcePattern.Matches(xaml).Select(match => match.Groups["key"].Value)];

    internal static IReadOnlyList<string> ViewLocalValueKeys(XDocument view) =>
        [.. view.Descendants()
            .Where(element => ValueElements.Contains(element.Name.LocalName, StringComparer.Ordinal))
            .Where(element => element.Attribute(Xaml + "Key") is not null)
            .Where(element => element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)))
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .OfType<string>()];

    private static IEnumerable<string> SourceViews(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    public static TheoryData<string, string> TokensByVariant()
    {
        TheoryData<string, string> data = [];
        foreach (string variant in new[] { "Dark", "Light" })
        {
            foreach (ThemeToken token in ThemeTokenCatalogue.All)
            {
                data.Add(variant, token.Name);
            }
        }

        return data;
    }

    private static HashSet<string> KeysOf(string variant)
    {
        XDocument document = XDocument.Load(Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "src", "NetPrints.Editor", "EditorStyles.axaml"));
        XElement dictionary = document.Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary" && (string?)element.Attribute(Xaml + "Key") == variant);
        return [.. dictionary.Elements().Select(element => (string?)element.Attribute(Xaml + "Key")).OfType<string>()];
    }

    [Fact]
    public void TheCatalogueNamesAreUniqueAndFollowAreaDotRole()
    {
        Assert.Equal(ThemeTokenCatalogue.All.Count, ThemeTokenCatalogue.All.Select(token => token.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ThemeTokenCatalogue.All, token => Assert.Matches(@"^[A-Z][A-Za-z]*(\.[A-Z][A-Za-z]*)+$", token.Name));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void EveryKeyOfTheThemeDictionariesIsInTheCatalogue(string variant)
    {
        string[] missing = [.. KeysOf(variant).Where(key => ThemeTokenCatalogue.Find(key) is null)];

        Assert.True(missing.Length == 0, $"Tokens of the {variant} dictionary missing from ThemeTokenCatalogue: {string.Join(", ", missing)}");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void EveryPerVariantTokenOfTheCatalogueIsInTheDictionaryOfEachVariant(string variant)
    {
        HashSet<string> keys = KeysOf(variant);
        string[] missing = [.. ThemeTokenCatalogue.All.Where(token => token.PerVariant && !keys.Contains(token.Name)).Select(token => token.Name)];

        Assert.True(missing.Length == 0, $"Catalogue tokens absent from the {variant} dictionary: {string.Join(", ", missing)}");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(TokensByVariant))]
    public void EveryCatalogueTokenResolvesToItsKindInBothVariants(string variant, string name)
    {
        ThemeToken token = ThemeTokenCatalogue.Find(name) ?? throw new InvalidOperationException(name);
        ThemeVariant theme = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        Assert.True(Application.Current is { } app && app.TryGetResource(name, theme, out object? value) && token.Accepts(value), $"{name} in {variant} is not a {token.Kind}");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheCatalogueCarriesTheWireTokensPerVariantAndTheInspectorTokens()
    {
        Assert.True(ThemeTokenCatalogue.Find("Wire.Opacity")?.PerVariant);
        Assert.True(ThemeTokenCatalogue.Find("Wire.Thickness")?.PerVariant);
        Assert.Contains(ThemeTokenCatalogue.All, token => token.Name.StartsWith("Inspector.", StringComparison.Ordinal));
        Assert.Equal(ThemeTokenKind.Shape, ThemeTokenCatalogue.Find("Pin.TypeShape")?.Kind);
    }

    [Fact]
    public void EveryKeyedResourceOfEditorStylesIsInTheCatalogue()
    {
        XDocument document = XDocument.Load(Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "src", "NetPrints.Editor", "EditorStyles.axaml"));
        string[] missing =
        [
            .. document.Descendants()
                .Where(element => element.Attribute(Xaml + "Key") is not null)
                .Where(element => !element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith("ThemeDictionaries", StringComparison.Ordinal)))
                .Where(element => element.Name.LocalName is not ("ControlTheme" or "FocusAdornerTemplate"))
                .Select(element => (string?)element.Attribute(Xaml + "Key"))
                .OfType<string>()
                .Where(key => !key.StartsWith("Pin.TypeShape.", StringComparison.Ordinal))
                .Where(key => ThemeTokenCatalogue.Find(key) is null),
        ];

        Assert.True(missing.Length == 0, $"Keyed resources of EditorStyles.axaml missing from ThemeTokenCatalogue: {string.Join(", ", missing)}");
    }

    [Fact]
    public void NoCatalogueTokenIsLookedUpWithStaticResource()
    {
        string root = LocalSdkLayout.FindRepositoryRoot();
        List<string> offenders = [];
        foreach (string file in SourceViews(root))
        {
            offenders.AddRange(StaticResourceKeys(File.ReadAllText(file))
                .Where(key => ThemeTokenCatalogue.Find(key) is not null)
                .Select(key => $"{Path.GetRelativePath(root, file)}: {key}"));
        }

        Assert.True(offenders.Count == 0, "Catalogue tokens looked up with StaticResource: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheStaticResourceGuardReadsTheMarkupAndTheElementSyntax()
    {
        const string Sample = """
            <StaticResource x:Key="Probe.Size" ResourceKey="Font.Body" />
            <TextBlock FontSize="{StaticResource Font.Title}" Margin="{StaticResource ResourceKey=Space.S}" />
            """;

        Assert.Equal(["Font.Body", "Font.Title", "Space.S"], StaticResourceKeys(Sample));
    }

    [Fact]
    public void ViewLocalValueKeysSeesEveryResourceForm()
    {
        const string Sample = """
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <UserControl.Resources>
                <ResourceDictionary>
                  <x:Double x:Key="A">4</x:Double>
                </ResourceDictionary>
              </UserControl.Resources>
              <Grid>
                <Grid.Resources>
                  <Thickness x:Key="B">2</Thickness>
                </Grid.Resources>
                <Border>
                  <Border.Resources>
                    <LinearGradientBrush x:Key="C" />
                  </Border.Resources>
                  <Border.Background>
                    <SolidColorBrush Color="Red" />
                  </Border.Background>
                </Border>
              </Grid>
            </UserControl>
            """;

        Assert.Equal(["A", "B", "C"], ViewLocalValueKeys(XDocument.Parse(Sample)));
    }

    [Fact]
    public void NoViewKeepsASizeOrColourOfItsOwn()
    {
        string root = LocalSdkLayout.FindRepositoryRoot();
        List<string> offenders = [];
        foreach (string file in SourceViews(root))
        {
            XDocument document = XDocument.Load(file);
            if (document.Root?.Name.LocalName is not ("UserControl" or "Window"))
            {
                continue;
            }

            offenders.AddRange(ViewLocalValueKeys(document).Select(key => $"{Path.GetRelativePath(root, file)}: {key}"));
        }

        Assert.True(offenders.Count == 0, "Views that keep their own size or colour instead of a catalogue token: " + string.Join(", ", offenders));
    }
}
