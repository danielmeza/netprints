using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>FR-006: an invalid settings value falls back to the section's defaults, is logged once, and never stops the start.</summary>
public sealed class SettingsValidationTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();
    private readonly List<string> messages = [];

    private string SettingsFile => Path.Combine(directory, "settings.json");

    public void Dispose() => TestPaths.TryDelete(directory);

    [Theory]
    [InlineData("""{"netprints":{"extensionPaths":["", "/x"]}}""", "extensionPaths")]
    [InlineData("""{"netprints":{"trustedProjects":["  "]}}""", "trustedProjects")]
    [InlineData("""{"netprints":{"enableAnimations":"maybe"}}""", "enableAnimations")]
    [InlineData("""{"netprints":{"language":"not a culture"}}""", "language")]
    [InlineData("""{"netprints":{"language":"zz-ZZ-ZZ-ZZ"}}""", "language")]
    [InlineData("""{"netprints":{"language":7}}""", "language")]
    public async Task AnInvalidNetPrintsSectionUsesItsDefaultsAndLogsTheKeyAndReason(string json, string key)
    {
        await File.WriteAllTextAsync(SettingsFile, json, TestContext.Current.CancellationToken);

        NetPrintsSettings settings = CreateStore().Get(NetPrintsSettings.Descriptor);

        AssertDefaults(settings);
        string message = Assert.Single(messages);
        Assert.Contains("netprints", message, StringComparison.Ordinal);
        Assert.Contains(key, message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"extensions":{"netprints.editor":{"theme":"purple"}}}""")]
    [InlineData("""{"extensions":{"netprints.editor":{"theme":7}}}""")]
    public async Task AnInvalidEditorSectionUsesItsDefaultsAndLogsTheKeyAndReason(string json)
    {
        await File.WriteAllTextAsync(SettingsFile, json, TestContext.Current.CancellationToken);

        EditorSettings settings = CreateStore().Get(EditorSettings.Descriptor);

        Assert.Equal(EditorTheme.Dark, settings.Theme);
        string message = Assert.Single(messages);
        Assert.Contains("netprints.editor", message, StringComparison.Ordinal);
        Assert.Contains("theme", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"run":{"stopTimeoutSeconds":0}}""", "stopTimeoutSeconds")]
    [InlineData("""{"run":{"stopTimeoutSeconds":601}}""", "stopTimeoutSeconds")]
    [InlineData("""{"run":{"stopTimeoutSeconds":-3}}""", "stopTimeoutSeconds")]
    [InlineData("""{"run":{"stopTimeoutSeconds":"soon"}}""", "stopTimeoutSeconds")]
    [InlineData("""{"run":{"linuxTerminal":"xterm -e\nrm -rf x"}}""", "linuxTerminal")]
    public async Task AnInvalidRunSectionUsesItsDefaultsAndLogsTheKeyAndReason(string json, string key)
    {
        await File.WriteAllTextAsync(SettingsFile, json, TestContext.Current.CancellationToken);

        RunSettings settings = CreateStore().Get(RunSettings.Descriptor);

        Assert.Equal(new RunSettings(), settings);
        string message = Assert.Single(messages);
        Assert.Contains("run", message, StringComparison.Ordinal);
        Assert.Contains(key, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AValidRunSectionIsReadFromTheTopLevelAndWrittenBack()
    {
        await File.WriteAllTextAsync(SettingsFile, """{"run":{"stopTimeoutSeconds":600,"linuxTerminal":"kitty -e"}}""", TestContext.Current.CancellationToken);
        JsonFileSettingsStore store = CreateStore();

        Assert.Equal(new RunSettings { StopTimeoutSeconds = 600, LinuxTerminal = "kitty -e" }, store.Get(RunSettings.Descriptor));

        await store.SetAsync(RunSettings.Descriptor, new RunSettings { StopTimeoutSeconds = 1 }, TestContext.Current.CancellationToken);

        string text = await File.ReadAllTextAsync(SettingsFile, TestContext.Current.CancellationToken);
        Assert.Contains("\n  \"run\": {", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"extensions\"", text, StringComparison.Ordinal);
        Assert.Empty(messages);
    }

    [Theory]
    [InlineData("""{"extensions":{"netprints.editor":{"darkPreset":"Not A Preset!"}}}""", "darkPreset")]
    [InlineData("""{"extensions":{"netprints.editor":{"darkPreset":""}}}""", "darkPreset")]
    [InlineData("""{"extensions":{"netprints.editor":{"lightPreset":"two words"}}}""", "lightPreset")]
    [InlineData("""{"extensions":{"netprints.editor":{"lightPreset":7}}}""", "lightPreset")]
    public async Task AnInvalidPresetIdUsesTheEditorDefaultsAndLogsTheKey(string json, string key)
    {
        await File.WriteAllTextAsync(SettingsFile, json, TestContext.Current.CancellationToken);

        EditorSettings settings = CreateStore().Get(EditorSettings.Descriptor);

        Assert.Equal(new EditorSettings(), settings);
        Assert.Contains(key, Assert.Single(messages), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APresetIdOfAnExtensionThemeIsAccepted()
    {
        await File.WriteAllTextAsync(
            SettingsFile,
            """{"extensions":{"netprints.editor":{"darkPreset":"com.acme.midnight","lightPreset":"netprints.classic-light"}}}""",
            TestContext.Current.CancellationToken);

        EditorSettings settings = CreateStore().Get(EditorSettings.Descriptor);

        Assert.Equal("com.acme.midnight", settings.DarkPreset);
        Assert.Equal("netprints.classic-light", settings.LightPreset);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ValidSectionsAreReadAndNothingIsLogged()
    {
        await File.WriteAllTextAsync(
            SettingsFile,
            """{"netprints":{"extensionPaths":["/ext"],"enableAnimations":false},"extensions":{"netprints.editor":{"theme":"light"}}}""",
            TestContext.Current.CancellationToken);
        JsonFileSettingsStore store = CreateStore();

        Assert.Equal(["/ext"], store.Get(NetPrintsSettings.Descriptor).ExtensionPaths);
        Assert.False(store.Get(NetPrintsSettings.Descriptor).EnableAnimations);
        Assert.Equal(EditorTheme.Light, store.Get(EditorSettings.Descriptor).Theme);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task OneInvalidSectionDoesNotAffectTheOther()
    {
        await File.WriteAllTextAsync(
            SettingsFile,
            """{"netprints":{"extensionPaths":[""]},"extensions":{"netprints.editor":{"theme":"light"}}}""",
            TestContext.Current.CancellationToken);
        JsonFileSettingsStore store = CreateStore();

        AssertDefaults(store.Get(NetPrintsSettings.Descriptor));
        Assert.Equal(EditorTheme.Light, store.Get(EditorSettings.Descriptor).Theme);
        Assert.Single(messages);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("es-MX")]
    [InlineData("zh-Hans-CN")]
    [InlineData("")]
    public async Task AValidLanguageIsReadAndNothingIsLogged(string language)
    {
        await File.WriteAllTextAsync(SettingsFile, "{\"netprints\":{\"language\":\"" + language + "\"}}", TestContext.Current.CancellationToken);

        Assert.Equal(language, CreateStore().Get(NetPrintsSettings.Descriptor).Language);
        Assert.Empty(messages);
    }

    private static void AssertDefaults(NetPrintsSettings settings)
    {
        Assert.Equal("", settings.Language);
        Assert.Empty(settings.ExtensionPaths);
        Assert.Empty(settings.TrustedProjects);
        Assert.True(settings.EnableAnimations);
    }

    private JsonFileSettingsStore CreateStore() => new(SettingsFile, new ListLogger<JsonFileSettingsStore>(messages));

    private sealed class ListLogger<T>(List<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }
}
