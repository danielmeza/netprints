using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Settings;

public sealed record SampleSettings
{
    public const string SectionId = "sample.section";

    [Description("Turns the sample on")]
    public bool Enabled { get; init; }

    [Range(1, 600)]
    [Description("Seconds to wait")]
    public int TimeoutSeconds { get; init; } = 5;

    [Description("A free text")]
    public string Greeting { get; init; } = "hello";

    [SettingChoices("sample.modes")]
    [Description("One of the modes")]
    public string Mode { get; init; } = "fast";

    [SettingChoices("sample.unknown")]
    public string Orphan { get; init; } = "";

    [SettingPath(PathKind.Folder)]
    [Description("A folder")]
    public string Home { get; init; } = "";

    [RequiresRestart]
    [SettingPath(PathKind.File)]
    [Description("Files to open")]
    public IReadOnlyList<string> Files { get; init; } = [];

    public SampleKind Kind { get; init; } = SampleKind.Small;

    public static ExtensionSettingsDescriptor<SampleSettings> Descriptor { get; } =
        new(SectionId, SampleSettingsJsonContext.Default.SampleSettings, new SampleSettings())
        {
            Validator = value => [.. new SampleSettingsValidator().Validate(null, value).Failures ?? []],
        };
}

[JsonConverter(typeof(JsonStringEnumConverter<SampleKind>))]
public enum SampleKind
{
    Small,
    Large,
}

[Microsoft.Extensions.Options.OptionsValidator]
public sealed partial class SampleSettingsValidator : Microsoft.Extensions.Options.IValidateOptions<SampleSettings>;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SampleSettings))]
public sealed partial class SampleSettingsJsonContext : JsonSerializerContext;

public sealed class SettingsFieldTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "netprints-settings-fields", Guid.NewGuid().ToString("N"));
    private readonly FakeFilePicker picker = new();

    private string FilePath => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private JsonFileSettingsStore NewStore() => new(FilePath, NullLogger<JsonFileSettingsStore>.Instance);

    private IReadOnlyDictionary<string, SettingsFieldViewModel> Fields(JsonFileSettingsStore store)
    {
        var section = new SettingsSection(SampleSettings.Descriptor, store);
        var services = new SettingsFieldServices(picker, id => id == "sample.modes" ? ["fast", "slow"] : null);
        return section.CreateFields(services).ToDictionary(field => field.Key);
    }

    [Fact]
    public void EachSchemaShapeGetsTheFieldOfTheDataModelTable()
    {
        var fields = Fields(NewStore());

        Assert.IsType<ToggleFieldViewModel>(fields["enabled"]);
        var number = Assert.IsType<NumberFieldViewModel>(fields["timeoutSeconds"]);
        Assert.Equal(1d, number.Minimum);
        Assert.Equal(600d, number.Maximum);
        Assert.IsType<TextFieldViewModel>(fields["greeting"]);
        var choice = Assert.IsType<ChoiceFieldViewModel>(fields["mode"]);
        Assert.Equal(["fast", "slow"], choice.Choices);
        Assert.Equal(["small", "large"], Assert.IsType<ChoiceFieldViewModel>(fields["kind"]).Choices.Select(text => text.ToLowerInvariant()));
        Assert.IsType<TextFieldViewModel>(fields["orphan"]);
        Assert.Equal(PathKind.Folder, Assert.IsType<PathFieldViewModel>(fields["home"]).Kind);
        var list = Assert.IsType<StringListFieldViewModel>(fields["files"]);
        Assert.Equal(PathKind.File, list.PathKind);
    }

    [Fact]
    public void ALabelIsTheNameInWordsAndTheHelpTheDescription()
    {
        var fields = Fields(NewStore());

        Assert.Equal("Timeout seconds", fields["timeoutSeconds"].Label);
        Assert.Equal("Seconds to wait", fields["timeoutSeconds"].Description);
        Assert.Equal("", fields["orphan"].Description);
    }

    [Fact]
    public void ALabelAndAHelpTextComeFromTheOwnersResourcesFirst()
    {
        var section = new SettingsSection(SampleSettings.Descriptor, NewStore(), [new SampleResources(), Strings.ResourceManager]);
        var services = new SettingsFieldServices(picker, id => id == "sample.modes" ? ["fast", "slow"] : null);

        IReadOnlyDictionary<string, SettingsFieldViewModel> fields = section.CreateFields(services).ToDictionary(field => field.Key);

        Assert.Equal("Greeting text", fields["greeting"].Label);
        Assert.Equal("Says hello", fields["greeting"].Description);
        Assert.Equal("Timeout seconds", fields["timeoutSeconds"].Label);
        Assert.Equal("Seconds to wait", fields["timeoutSeconds"].Description);
    }

    [Fact]
    public async Task AnInvalidValueShowsTheValidatorMessageAndIsNotSaved()
    {
        JsonFileSettingsStore store = NewStore();
        var number = Assert.IsType<NumberFieldViewModel>(Fields(store)["timeoutSeconds"]);

        number.Text = "0";
        await number.CommitCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(number.Error));
        Assert.Equal(5, store.Get(SampleSettings.Descriptor).TimeoutSeconds);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task ATextThatIsNotANumberIsRefusedWithoutTheValidator()
    {
        JsonFileSettingsStore store = NewStore();
        var number = Assert.IsType<NumberFieldViewModel>(Fields(store)["timeoutSeconds"]);

        number.Text = "many";
        await number.CommitCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(number.Error));
        Assert.Equal(5, store.Get(SampleSettings.Descriptor).TimeoutSeconds);
    }

    [Fact]
    public async Task AValidValueIsSavedAtOnceAndClearsTheError()
    {
        JsonFileSettingsStore store = NewStore();
        var number = Assert.IsType<NumberFieldViewModel>(Fields(store)["timeoutSeconds"]);
        number.Text = "0";
        await number.CommitCommand.ExecuteAsync(null);

        number.Text = "30";
        await number.CommitCommand.ExecuteAsync(null);

        Assert.Null(number.Error);
        Assert.Equal(30, store.Get(SampleSettings.Descriptor).TimeoutSeconds);
        Assert.Equal(30, new JsonFileSettingsStore(FilePath, NullLogger<JsonFileSettingsStore>.Instance).Get(SampleSettings.Descriptor).TimeoutSeconds);
    }

    [Fact]
    public async Task AToggleAChoiceAndATextSaveThroughTheirCommands()
    {
        JsonFileSettingsStore store = NewStore();
        var fields = Fields(store);

        await ((ToggleFieldViewModel)fields["enabled"]).ToggleCommand.ExecuteAsync(null);
        await ((ChoiceFieldViewModel)fields["mode"]).ChooseCommand.ExecuteAsync("slow");
        await ((ChoiceFieldViewModel)fields["kind"]).ChooseCommand.ExecuteAsync("large");
        var greeting = (TextFieldViewModel)fields["greeting"];
        greeting.Text = "hola";
        await greeting.CommitCommand.ExecuteAsync(null);

        SampleSettings saved = store.Get(SampleSettings.Descriptor);
        Assert.True(saved.Enabled);
        Assert.Equal("slow", saved.Mode);
        Assert.Equal(SampleKind.Large, saved.Kind);
        Assert.Equal("hola", saved.Greeting);
    }

    [Fact]
    public async Task ARestartFieldSaysSoAndRaisesSavedOnlyWhenTheValueChanged()
    {
        JsonFileSettingsStore store = NewStore();
        var list = Assert.IsType<StringListFieldViewModel>(Fields(store)["files"]);
        var saved = new List<SettingsFieldViewModel>();
        list.Saved += (_, _) => saved.Add(list);

        list.NewItem = "/tmp/a.nproj";
        await list.AddCommand.ExecuteAsync(null);
        await list.CommitCommand.ExecuteAsync(null);

        Assert.True(list.RequiresRestart);
        Assert.Single(saved);
        Assert.Equal(["/tmp/a.nproj"], store.Get(SampleSettings.Descriptor).Files);
    }

    [Fact]
    public async Task AStringListAddsRemovesAndReorders()
    {
        JsonFileSettingsStore store = NewStore();
        var list = Assert.IsType<StringListFieldViewModel>(Fields(store)["files"]);

        foreach (string path in new[] { "a", "b", "c" })
        {
            list.NewItem = path;
            await list.AddCommand.ExecuteAsync(null);
        }

        await list.MoveUpCommand.ExecuteAsync(list.Items[2]);
        await list.RemoveCommand.ExecuteAsync(list.Items[0]);

        Assert.Equal(["c", "b"], list.Items.Select(item => item.Text));
        Assert.Equal(["c", "b"], store.Get(SampleSettings.Descriptor).Files);
    }

    [Fact]
    public async Task BrowsingFillsAPathFieldFromThePicker()
    {
        JsonFileSettingsStore store = NewStore();
        var home = Assert.IsType<PathFieldViewModel>(Fields(store)["home"]);
        picker.FolderAnswers.Enqueue("/work/netprints");

        await home.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("/work/netprints", home.Text);
        Assert.Equal("/work/netprints", store.Get(SampleSettings.Descriptor).Home);
    }

    [Fact]
    public async Task ACancelledBrowseChangesNothing()
    {
        JsonFileSettingsStore store = NewStore();
        var home = Assert.IsType<PathFieldViewModel>(Fields(store)["home"]);
        picker.FolderAnswers.Enqueue(null);

        await home.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("", home.Text);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task AnUnchangedValueWritesNothing()
    {
        JsonFileSettingsStore store = NewStore();
        var greeting = Assert.IsType<TextFieldViewModel>(Fields(store)["greeting"]);

        await greeting.CommitCommand.ExecuteAsync(null);

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void SearchMatchesTheLabelAndTheDescriptionIgnoringCase()
    {
        var fields = Fields(NewStore());

        Assert.True(fields["timeoutSeconds"].Matches("TIMEOUT"));
        Assert.True(fields["timeoutSeconds"].Matches("seconds to"));
        Assert.False(fields["timeoutSeconds"].Matches("folder"));
        Assert.True(fields["greeting"].Matches(""));
    }

    private sealed class SampleResources : ResourceManager
    {
        private static readonly Dictionary<string, string> Entries = new(StringComparer.Ordinal)
        {
            ["Settings.sample.section.greeting.Label"] = "Greeting text",
            ["Settings.sample.section.greeting.Description"] = "Says hello",
        };

        protected override ResourceSet? InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) => new FakeSet(Entries);
    }

    private sealed class FakeSet(Dictionary<string, string> entries) : ResourceSet
    {
        public override string? GetString(string name) => entries.GetValueOrDefault(name);

        public override string? GetString(string name, bool ignoreCase) => entries.GetValueOrDefault(name);
    }
}
