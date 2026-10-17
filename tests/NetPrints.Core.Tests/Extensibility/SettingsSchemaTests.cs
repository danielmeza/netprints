using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-021, FR-040, research R6: the exported schema carries the NetPrints keywords of the settings metadata attributes.</summary>
public partial class SettingsSchemaTests
{
    private static JsonObject Property(string name)
    {
        var schema = (JsonObject)SettingsSchema.Export(FxSettingsContext.Default.FxSettings);
        var properties = Assert.IsType<JsonObject>(schema["properties"]);
        return Assert.IsType<JsonObject>(properties[name]);
    }

    [Fact]
    public void RequiresRestartWritesTheRestartKeyword()
    {
        Assert.True((bool?)Property("cachePath")["x-netprints-restart"]);
        Assert.Null(Property("level")["x-netprints-restart"]);
    }

    [Fact]
    public void SettingPathWritesFileOrFolder()
    {
        Assert.Equal("folder", (string?)Property("cachePath")["x-netprints-path"]);
        Assert.Equal("file", (string?)Property("tool")["x-netprints-path"]);
        Assert.Null(Property("level")["x-netprints-path"]);
    }

    [Fact]
    public void DescriptionBecomesTheDescriptionKeyword()
    {
        Assert.Equal("Where the cache lives.", (string?)Property("cachePath")["description"]);
        Assert.Null(Property("level")["description"]);
    }

    [Fact]
    public void ChoicesWriteTheProviderId()
    {
        Assert.Equal("fx.levels", (string?)Property("level")["x-netprints-choices"]);
    }

    internal sealed record FxSettings
    {
        [RequiresRestart]
        [SettingPath(PathKind.Folder)]
        [Description("Where the cache lives.")]
        public string CachePath { get; init; } = "";

        [SettingPath(PathKind.File)]
        public string Tool { get; init; } = "";

        [SettingChoices("fx.levels")]
        public string Level { get; init; } = "";
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(FxSettings))]
    internal sealed partial class FxSettingsContext : JsonSerializerContext;
}
