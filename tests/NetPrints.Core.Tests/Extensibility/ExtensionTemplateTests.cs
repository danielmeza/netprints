using System;
using System.IO;
using System.Text.Json;
using NetPrints.Extensibility;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-079: the sources of the netprints-extension template stay consistent with the API (the install, build, test and pack run is in scripts/verify-packages.sh).</summary>
public class ExtensionTemplateTests
{
    private static string Template => Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Templates", "content", "netprints-extension");

    [Fact]
    public void TheManifestTargetsTheCurrentApiVersion()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Template, "MyExtension", "netprints-extension.json")));

        Assert.Equal($"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}", manifest.RootElement.GetProperty("netprintsApi").GetString());
    }

    [Fact]
    public void TheExtensionIdIsTheSameInTheManifestTheProjectAndTheCode()
    {
        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(Path.Combine(Template, ".template.config", "template.json")));
        string id = config.RootElement.GetProperty("symbols").GetProperty("ExtensionId").GetProperty("replaces").GetString() ?? string.Empty;
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Template, "MyExtension", "netprints-extension.json")));

        Assert.Equal(id, manifest.RootElement.GetProperty("id").GetString());
        Assert.Contains($"<NetPrintsExtensionId>{id}</NetPrintsExtensionId>", File.ReadAllText(Path.Combine(Template, "MyExtension", "MyExtension.csproj")), StringComparison.Ordinal);
        Assert.Contains($"\"{id}\"", File.ReadAllText(Path.Combine(Template, "MyExtension", "Extension.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void BothProjectsTakeTheNetPrintsVersionFromTheOneSymbol()
    {
        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(Path.Combine(Template, ".template.config", "template.json")));
        string token = config.RootElement.GetProperty("symbols").GetProperty("NetPrintsVersion").GetProperty("replaces").GetString() ?? string.Empty;

        Assert.Contains($"Version=\"{token}\"", File.ReadAllText(Path.Combine(Template, "MyExtension", "MyExtension.csproj")), StringComparison.Ordinal);
        Assert.Contains($"Version=\"{token}\"", File.ReadAllText(Path.Combine(Template, "MyExtension.Tests", "MyExtension.Tests.csproj")), StringComparison.Ordinal);
    }
}
