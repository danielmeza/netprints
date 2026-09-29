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
