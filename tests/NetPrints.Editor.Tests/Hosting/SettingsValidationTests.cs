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
