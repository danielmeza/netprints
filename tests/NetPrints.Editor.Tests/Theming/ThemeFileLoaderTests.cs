using NetPrints.Editor.Theming;

namespace NetPrints.Editor.Tests.Theming;

public class ThemeFileLoaderTests
{
    private static string File(string tokens, string head = "\"schemaVersion\": 1, \"id\": \"sample.dark\", \"label\": \"Sample\", \"base\": \"dark\"") =>
        "{" + head + ", \"tokens\": {" + tokens + "}}";

    [Fact]
    public void AValidFileLoadsItsTokensWithoutWarnings()
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File("\"Canvas.Background\": \"#FF000000\", \"Wire.Opacity\": \"0.5\", \"Pin.TypeShape\": \"triangle\""));

        ThemePreset preset = Assert.IsType<ThemePreset>(result.Preset);
        Assert.Empty(result.Warnings);
        Assert.Equal("sample.dark", preset.Id);
        Assert.Equal("Sample", preset.Label);
        Assert.Equal(ThemeBase.Dark, preset.Base);
        Assert.Null(preset.SemiVariant);
        Assert.Equal(3, preset.Tokens.Count);
    }

    [Fact]
    public void AnUnknownTokenIsWarnedAboutAndIgnored()
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File("\"No.Such.Token\": \"#FF000000\", \"Canvas.Background\": \"#FF000000\""));

        Assert.Equal(["Canvas.Background"], result.Preset?.Tokens.Keys);
        Assert.Contains("No.Such.Token", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void ABadValueIsWarnedAboutAndKeepsTheBase()
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File("\"Canvas.Background\": \"not a colour\", \"Wire.Opacity\": \"0.5\""));

        Assert.Equal(["Wire.Opacity"], result.Preset?.Tokens.Keys);
        Assert.Contains("Canvas.Background", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\": 2, \"id\": \"a.b\", \"label\": \"A\", \"base\": \"dark\", \"tokens\": {}}")]
    [InlineData("{\"schemaVersion\": 1, \"id\": \"Bad Id\", \"label\": \"A\", \"base\": \"dark\", \"tokens\": {}}")]
    [InlineData("{\"schemaVersion\": 1, \"id\": \"a.b\", \"label\": \"\", \"base\": \"dark\", \"tokens\": {}}")]
    [InlineData("{\"schemaVersion\": 1, \"id\": \"a.b\", \"label\": \"A\", \"base\": \"sepia\", \"tokens\": {}}")]
    public void AFileThatCannotBeUsedIsSkippedWithAWarning(string json)
    {
        ThemeLoadResult result = ThemeFileLoader.Load(json);

        Assert.Null(result.Preset);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void AFileOverTheSizeCapIsSkipped()
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File(string.Empty, "\"schemaVersion\": 1, \"id\": \"a.b\", \"label\": \"" + new string('x', ThemeFileLoader.MaxLength) + "\", \"base\": \"dark\""));

        Assert.Null(result.Preset);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("Aquatic", "Aquatic")]
    [InlineData("NightSky", "NightSky")]
    public void AKnownSemiVariantIsKept(string name, string expected)
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File(string.Empty, "\"schemaVersion\": 1, \"id\": \"a.b\", \"label\": \"A\", \"base\": \"dark\", \"semiVariant\": \"" + name + "\""));

        Assert.Equal(expected, result.Preset?.SemiVariant);
    }

    [Fact]
    public void AnUnknownSemiVariantIsWarnedAboutAndDropped()
    {
        ThemeLoadResult result = ThemeFileLoader.Load(File(string.Empty, "\"schemaVersion\": 1, \"id\": \"a.b\", \"label\": \"A\", \"base\": \"dark\", \"semiVariant\": \"Neon\""));

        Assert.Null(result.Preset?.SemiVariant);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("Canvas.Background", "#FF000000", true)]
    [InlineData("Canvas.Background", "#000", true)]
    [InlineData("Node.CardBackground", "#AA202020", true)]
    [InlineData("Wire.Opacity", "0.78", true)]
    [InlineData("Wire.Opacity", "x", false)]
    [InlineData("Node.CardShadow", "4 4 8 0 #80000000", true)]
    [InlineData("Node.CardShadow", "lots", false)]
    [InlineData("Pin.TypeShape", "diamond", true)]
    [InlineData("Pin.TypeShape", "M0,0 L1,1", false)]
    public void ValuesAreParsedByTheKindOfTheToken(string name, string text, bool valid)
    {
        ThemeToken token = ThemeTokenCatalogue.Find(name) ?? throw new InvalidOperationException(name);

        Assert.Equal(valid, ThemeValueParser.TryParse(token, text, out object? value));
        Assert.Equal(valid, value is not null);
    }
}
