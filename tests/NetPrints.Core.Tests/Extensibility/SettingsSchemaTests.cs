using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

    [Fact]
    public void RangeWritesMinimumAndMaximum()
    {
        JsonObject timeout = Property("timeout");

        Assert.Equal(1, (int?)timeout["minimum"]);
        Assert.Equal(600, (int?)timeout["maximum"]);
        Assert.Null(Property("level")["minimum"]);
    }

    [Fact]
    public void LengthAndPatternAttributesWriteTheirKeywords()
    {
        JsonObject label = Property("label");

        Assert.Equal(2, (int?)label["minLength"]);
        Assert.Equal(40, (int?)label["maxLength"]);
        Assert.Equal("^[a-z]+$", (string?)label["pattern"]);
    }

    [Fact]
    public void ARequiredStringNeedsAtLeastOneCharacterUnlessEmptyIsAllowed()
    {
        Assert.Equal(1, (int?)Property("needed")["minLength"]);
        Assert.Null(Property("mayBeEmpty")["minLength"]);
        var schema = (JsonObject)SettingsSchema.Export(FxSettingsContext.Default.FxSettings);
        Assert.Contains(Assert.IsType<JsonArray>(schema["required"]), name => (string?)name == "mayBeEmpty");
    }

    internal sealed record FxSettings
    {
        [Required]
        public string Needed { get; init; } = "x";

        [Required(AllowEmptyStrings = true)]
        public string MayBeEmpty { get; init; } = "";

        [Range(1, 600)]
        public int Timeout { get; init; } = 5;

        [MinLength(2)]
        [StringLength(40)]
        [RegularExpression("^[a-z]+$")]
        public string Label { get; init; } = "ab";

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
