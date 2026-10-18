using NetPrints.Editor.State;
using NetPrints.Editor.Theming;

namespace NetPrints.Editor.Tests.Theming;

public class ThemePresetsTests
{
    [Fact]
    public void TheBuiltInPresetsLoadWithoutWarnings()
    {
        Assert.Equal(["Classic Dark", "High contrast", "High contrast", "NetPrints Dark", "NetPrints Light"], ThemePresets.BuiltIn.Select(preset => preset.Label).Order());
        Assert.Equal(ThemePresets.BuiltIn.Count, ThemePresets.BuiltIn.Select(preset => preset.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(ThemePresets.Warnings);
    }

    [Fact]
    public void EachVariantListsItsPresetsAndTheDefaultsComeFirst()
    {
        Assert.Equal([EditorSettings.DefaultDarkPreset, "classic.dark", "high-contrast.dark"], ThemePresets.IdsFor(ThemeBase.Dark));
        Assert.Equal([EditorSettings.DefaultLightPreset, "high-contrast.light"], ThemePresets.IdsFor(ThemeBase.Light));
    }

    [Theory]
    [InlineData(ThemeBase.Dark, "classic.dark", "classic.dark", true)]
    [InlineData(ThemeBase.Dark, "no.such", "netprints.dark", false)]
    [InlineData(ThemeBase.Dark, "netprints.light", "netprints.dark", false)]
    [InlineData(ThemeBase.Light, "classic.dark", "netprints.light", false)]
    [InlineData(ThemeBase.Light, "high-contrast.light", "high-contrast.light", true)]
    public void AnUnknownPresetFallsBackToTheDefaultOfTheVariant(ThemeBase variant, string id, string expected, bool known)
    {
        ThemePreset preset = ThemePresets.Resolve(variant, id, out bool found);

        Assert.Equal(expected, preset.Id);
        Assert.Equal(known, found);
    }

    [Fact]
    public void HighContrastNamesSemisVariantOfItsSlot()
    {
        Assert.Equal("Aquatic", ThemePresets.Resolve(ThemeBase.Dark, "high-contrast.dark", out _).SemiVariant);
        Assert.Equal("Desert", ThemePresets.Resolve(ThemeBase.Light, "high-contrast.light", out _).SemiVariant);
    }

    [Fact]
    public void ClassicDarkIsTheOldTrio()
    {
        IReadOnlyDictionary<string, string> tokens = ThemePresets.Resolve(ThemeBase.Dark, "classic.dark", out _).Tokens;

        Assert.Equal("triangle", tokens["Pin.TypeShape"]);
        Assert.Equal("#FF000000", tokens["Canvas.Background"]);
        Assert.Equal("#AA202020", tokens["Node.CardBackground"]);
    }
}
