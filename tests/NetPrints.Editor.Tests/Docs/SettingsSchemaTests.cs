using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Architecture;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>The schema file and the settings table of the guide are the ones generated from the built-in sections (FR-040, research R6, S10).</summary>
public sealed class SettingsSchemaTests
{
    /// <summary>The variable that rewrites the schema file and the table of the guide.</summary>
    public const string UpdateVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    private static string SchemaPath => Path.Combine(RepositoryPaths.Root(), "schemas", "settings.v1.schema.json");

    private static string GuidePath => Path.Combine(RepositoryPaths.Root(), "docs", "guide", "editor", "settings.md");

    private static JsonObject Section(JsonObject schema, string key)
    {
        JsonObject properties = Assert.IsType<JsonObject>(schema["properties"]);
        if (key == EditorSettings.SectionId)
        {
            JsonObject extensions = Assert.IsType<JsonObject>(properties["extensions"]);
            return Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(extensions["properties"])[key]);
        }

        return Assert.IsType<JsonObject>(properties[key]);
    }

    private static JsonObject Property(string section, string name)
    {
        var schema = Assert.IsType<JsonObject>(JsonNode.Parse(SettingsSchemaFile.Generate()));
        return Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(Section(schema, section)["properties"])[name]);
    }

    [Fact]
    public void TheSchemaFileIsTheOneGeneratedFromTheBuiltInSections()
    {
        string generated = SettingsSchemaFile.Generate();
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(SchemaPath, generated);
        }

        Assert.True(File.Exists(SchemaPath), $"Missing {SchemaPath}; regenerate with {UpdateVariable}=1");
        Assert.Equal(File.ReadAllText(SchemaPath).ReplaceLineEndings("\n"), generated);
    }

    [Fact]
    public void TheSchemaDeclaresItsOwnIdAndTheSectionsOfThisVersion()
    {
        var schema = Assert.IsType<JsonObject>(JsonNode.Parse(SettingsSchemaFile.Generate()));

        Assert.Equal(SettingsSchemaFile.Id, (string?)schema["$id"]);
        Assert.NotNull(Section(schema, NetPrintsSettings.SectionId));
        Assert.NotNull(Section(schema, RunSettings.SectionId));
        Assert.NotNull(Section(schema, EditorSettings.SectionId));
        Assert.NotNull(Section(schema, KeyboardSettings.SectionId));
        Assert.True((bool?)schema["additionalProperties"]);
    }

    [Fact]
    public void TheDataAnnotationRangesAppearInTheSchema()
    {
        JsonObject timeout = Property(RunSettings.SectionId, "stopTimeoutSeconds");
        JsonObject terminal = Property(RunSettings.SectionId, "linuxTerminal");
        JsonObject preset = Property(EditorSettings.SectionId, "darkPreset");

        Assert.Equal(1, (int?)timeout["minimum"]);
        Assert.Equal(600, (int?)timeout["maximum"]);
        Assert.Equal(512, (int?)terminal["maxLength"]);
        Assert.NotNull(terminal["pattern"]);
        Assert.Equal(1, (int?)preset["minLength"]);
        Assert.NotNull(preset["pattern"]);
    }

    [Fact]
    public void EveryNetPrintsKeywordAppearsOnTheSettingsThatCarryIt()
    {
        Assert.True((bool?)Property(NetPrintsSettings.SectionId, "language")[SettingsSchema.RestartKeyword]);
        Assert.True((bool?)Property(NetPrintsSettings.SectionId, "extensionPaths")[SettingsSchema.RestartKeyword]);
        Assert.Equal("folder", (string?)Property(NetPrintsSettings.SectionId, "extensionPaths")[SettingsSchema.PathKeyword]);
        Assert.Equal("file", (string?)Property(NetPrintsSettings.SectionId, "trustedProjects")[SettingsSchema.PathKeyword]);
        Assert.Equal(EditorSettings.DarkPresetChoices, (string?)Property(EditorSettings.SectionId, "darkPreset")[SettingsSchema.ChoicesKeyword]);
        Assert.Equal(EditorSettings.LightPresetChoices, (string?)Property(EditorSettings.SectionId, "lightPreset")[SettingsSchema.ChoicesKeyword]);
    }

    [Fact]
    public void EverySettingHasADescriptionInTheSchema()
    {
        var schema = Assert.IsType<JsonObject>(JsonNode.Parse(SettingsSchemaFile.Generate()));
        foreach (string key in new[] { NetPrintsSettings.SectionId, RunSettings.SectionId, EditorSettings.SectionId, KeyboardSettings.SectionId })
        {
            foreach (KeyValuePair<string, JsonNode?> property in Assert.IsType<JsonObject>(Section(schema, key)["properties"]))
            {
                Assert.True(!string.IsNullOrWhiteSpace((string?)property.Value?["description"]), $"{key}.{property.Key} has no description");
            }
        }
    }

    [Fact]
    public void TheTableInTheGuideIsTheOneGeneratedFromTheSchema()
    {
        string table = SettingsSchemaFile.GenerateTable();
        string guide = File.ReadAllText(GuidePath).ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            guide = SettingsSchemaFile.Replace(guide, table);
            File.WriteAllText(GuidePath, guide);
        }

        Assert.Equal(table, SettingsSchemaFile.TableOf(guide));
    }

    [Fact]
    public void TheTableHasARowForEverySetting()
    {
        string table = SettingsSchemaFile.GenerateTable();

        Assert.Contains("| `run.stopTimeoutSeconds` | integer | `5` | 1 to 600 |", table, StringComparison.Ordinal);
        Assert.Contains("| `netprints.editor.darkPreset` | string | `netprints.dark` |", table, StringComparison.Ordinal);
        Assert.Contains("| `netprints.language` | string | empty |", table, StringComparison.Ordinal);
        Assert.Contains("| `netprints.extensionPaths` | list of string | empty |", table, StringComparison.Ordinal);
        Assert.Contains("| `keyboard.bindings` |", table, StringComparison.Ordinal);
    }

    [Fact]
    public void AStaleRowIsADifferenceAndReplacingTheTableTouchesNothingElse()
    {
        string table = SettingsSchemaFile.GenerateTable();
        string guide = $"Before\n\n{SettingsSchemaFile.Begin}\n\n{table}\n\n{SettingsSchemaFile.End}\n\nAfter\n";
        string stale = guide.Replace("| `run.stopTimeoutSeconds` | integer | `5` |", "| `run.stopTimeoutSeconds` | integer | `6` |", StringComparison.Ordinal);

        Assert.NotEqual(table, SettingsSchemaFile.TableOf(stale));
        Assert.Equal(table, SettingsSchemaFile.TableOf(guide));
        Assert.Equal(guide, SettingsSchemaFile.Replace(stale, table));
    }

    [Fact]
    public void TheExampleFileHoldsEverySettingAndTheStoreReadsItWithoutComplaint()
    {
        string path = Path.Combine(RepositoryPaths.Root(), "tests", "Fixtures", "Settings", "example.settings.json");
        var store = new JsonFileSettingsStore(path, NullLogger<JsonFileSettingsStore>.Instance);

        Assert.Equal("es-MX", store.Get(NetPrintsSettings.Descriptor).Language);
        Assert.Equal(600, store.Get(RunSettings.Descriptor).StopTimeoutSeconds);
        Assert.Equal("light", store.Get(EditorSettings.Descriptor).Theme.ToString().ToLowerInvariant());
        Assert.Equal("netprints.command.save", Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings).Command);
        Assert.Empty(store.Issues);
    }
}
