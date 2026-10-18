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
}
