using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.State;

/// <summary>FR-082: the theme choice and where it is kept.</summary>
public sealed class EditorSettingsTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();

    private string SettingsFile => Path.Combine(directory, "settings.json");

    private JsonFileSettingsStore CreateStore() => new(SettingsFile, NullLogger<JsonFileSettingsStore>.Instance);

    public void Dispose() => TestPaths.TryDelete(directory);

    [Fact]
    public void TheThemeIsDarkByDefault()
    {
        Assert.Equal(EditorTheme.Dark, CreateStore().Get(EditorSettings.Descriptor).Theme);
        Assert.Equal(EditorTheme.Dark, new EditorSettings().Theme);
    }

    [Theory]
    [InlineData(EditorTheme.Light, "light")]
    [InlineData(EditorTheme.System, "system")]
    [InlineData(EditorTheme.Dark, "dark")]
    public async Task TheChoiceIsStoredUnderNetprintsEditorAndSurvivesARestart(EditorTheme theme, string stored)
    {
        await CreateStore().SetAsync(EditorSettings.Descriptor, new EditorSettings { Theme = theme }, TestContext.Current.CancellationToken);

        string json = await File.ReadAllTextAsync(SettingsFile, TestContext.Current.CancellationToken);
        Assert.Contains("\"netprints.editor\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"theme\": \"{stored}\"", json, StringComparison.Ordinal);
        Assert.Equal(theme, CreateStore().Get(EditorSettings.Descriptor).Theme);
    }

    [Fact]
    public async Task AnUnknownThemeNameFallsBackToDark()
    {
        await File.WriteAllTextAsync(SettingsFile, """{"extensions":{"netprints.editor":{"theme":"purple"}}}""", TestContext.Current.CancellationToken);

        Assert.Equal(EditorTheme.Dark, CreateStore().Get(EditorSettings.Descriptor).Theme);
    }

    [Theory]
    [InlineData(EditorTheme.Dark)]
    [InlineData(EditorTheme.Light)]
    [InlineData(EditorTheme.System)]
    public async Task ThePickerCommandsAskTheShellToSwitchToTheirTheme(EditorTheme theme)
    {
        var shell = new FakeShell();
        var handler = new ThemeCommandHandler(theme);

        Assert.True(handler.CanExecute(shell.Context()));
        await handler.ExecuteAsync(shell.Context(), TestContext.Current.CancellationToken);

        Assert.Equal([$"SetTheme:{theme}"], shell.Project.Calls);
    }
}
