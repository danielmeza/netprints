using System.IO;
using System.Text;
using NetPrints.Extensibility.Loading;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>Parsing of <c>netprints-extension.json</c> (extension-points.md §8).</summary>
public class ExtensionManifestTests
{
    private static ExtensionManifest Parse(string json) =>
        ExtensionManifest.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)), "/x/netprints-extension.json");

    [Fact]
    public void ParsesTheDocumentedShape()
    {
        var manifest = Parse("""
            { "id": "com.example.sample", "name": "Sample extension", "version": "1.0.0",
              "assembly": "Example.NetPrints.Sample.dll", "netprintsApi": "1.0", "dependsOn": ["a", "b"] }
            """);

        Assert.Equal("com.example.sample", manifest.Id);
        Assert.Equal("Sample extension", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Example.NetPrints.Sample.dll", manifest.Assembly);
        Assert.Equal("1.0", manifest.NetprintsApi);
        Assert.Equal(new System.Version(1, 0), manifest.ApiVersion);
        Assert.Equal(["a", "b"], manifest.DependsOn);
    }

    [Fact]
    public void DependsOnMayBeOmittedAndUnknownPropertiesAreIgnored()
    {
        var manifest = Parse("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0", "future": 1 }""");

        Assert.Empty(manifest.DependsOn);
    }

    [Fact]
    public void ThemesAreAcceptedFromApi04()
    {
        var manifest = Parse("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "0.4", "themes": ["themes/one.nptheme.json", "two.nptheme.json"] }""");

        Assert.Equal(["themes/one.nptheme.json", "two.nptheme.json"], manifest.Themes);
        Assert.Empty(manifest.UnknownFields);
    }

    [Fact]
    public void ThemesAreEmptyWhenOmitted()
    {
        var manifest = Parse("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "0.4" }""");

        Assert.Empty(manifest.Themes);
    }

    [Theory]
    [InlineData("0.3")]
    [InlineData("0.0")]
    public void ThemesWithAnOlderApiAreNpx001(string api)
    {
        var exception = Assert.Throws<ExtensionManifestException>(
            () => Parse($$"""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "{{api}}", "themes": ["a.nptheme.json"] }"""));

        Assert.Equal("NPX001", exception.Code);
        Assert.Contains("themes", exception.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "0.4", "themes": "a.nptheme.json" }""")]
    [InlineData("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "0.4", "themes": [1] }""")]
    public void ThemesThatAreNotStringArraysAreNpx001(string json)
    {
        Assert.Equal("NPX001", Assert.Throws<ExtensionManifestException>(() => Parse(json)).Code);
    }

    [Fact]
    public void UnknownFieldsAreIgnoredAndListed()
    {
        var manifest = Parse("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "0.4", "future": 1, "other": {} }""");

        Assert.Equal(["future", "other"], manifest.UnknownFields);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0" }""")]
    [InlineData("""{ "id": "a", "name": "", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0" }""")]
    [InlineData("""{ "id": "a", "name": "A", "version": 1, "assembly": "a.dll", "netprintsApi": "1.0" }""")]
    [InlineData("""{ "id": "a/b", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0" }""")]
    [InlineData("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1" }""")]
    [InlineData("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0", "dependsOn": "b" }""")]
    [InlineData("""{ "id": "a", "name": "A", "version": "1", "assembly": "a.dll", "netprintsApi": "1.0", "dependsOn": [1] }""")]
    public void InvalidManifestsThrowNpx001(string json)
    {
        var exception = Assert.Throws<ExtensionManifestException>(() => Parse(json));

        Assert.Equal("NPX001", exception.Code);
        Assert.Equal("/x/netprints-extension.json", exception.ManifestPath);
        Assert.Contains("/x/netprints-extension.json", exception.Message);
    }
}
