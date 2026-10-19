using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Settings;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Settings;

/// <summary>FR-045 to FR-047, US6: the Keyboard shortcuts page lists, filters and edits the key bindings.</summary>
public sealed class KeyboardShortcutsPageTests : IDisposable
{
    private const string Alpha = "netprints.command.alpha";
    private const string Beta = "netprints.command.beta";
    private const string Gamma = "ext.one.gamma";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "netprints-keyboard-page", Guid.NewGuid().ToString("N"));
    private readonly List<KeyboardShortcutsItemViewModel> pages = [];

    private string FilePath => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        foreach (KeyboardShortcutsItemViewModel page in pages)
        {
            page.Dispose();
        }

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CommandDescriptor Command(string id, string label, CommandScope scope, params string[] gestures) =>
        new(id, label, new NoopHandler(), DefaultGestures: [.. gestures.Select(text => CommandGesture.Parse(text, null))], Scope: scope);

    private JsonFileSettingsStore NewStore() => new(FilePath, NullLogger<JsonFileSettingsStore>.Instance);

    private static ContributionRegistry NewRegistry(JsonFileSettingsStore store)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(Command(Alpha, "Alpha", CommandScope.Global, "Ctrl+Alt+A"));
        registry.AddCommand(Command(Beta, "Beta", CommandScope.Global, "Ctrl+Alt+B", "Ctrl+Alt+C"));
        registry.For("ext.one", "Extension One").AddCommand(Command(Gamma, "Gamma", CommandScope.Graph, "Delete"));
        registry.SetUserBindings(store.Get(KeyboardSettings.Descriptor));
        registry.Freeze();
        return registry;
    }

    private KeyboardShortcutsItemViewModel Open(ContributionRegistry registry, JsonFileSettingsStore store)
    {
        var page = new KeyboardShortcutsItemViewModel(registry, store);
        pages.Add(page);
        return page;
    }

    private static ShortcutEditRowViewModel Row(KeyboardShortcutsItemViewModel page, string id) => page.Rows.Single(row => row.CommandId == id);

    private static async Task Record(KeyboardShortcutsItemViewModel page, string id, string gesture)
    {
        Row(page, id).ToggleRecordingCommand.Execute(null);
        await page.CaptureCommand.ExecuteAsync(gesture);
    }

    [Fact]
    public void ThereIsOneRowPerCommandWithLabelOwnerScopeDefaultAndCurrentGestures()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        Assert.Equal([Alpha, Beta, Gamma], page.Rows.Select(row => row.CommandId));
        ShortcutEditRowViewModel beta = Row(page, Beta);
        Assert.Equal("Beta", beta.Label);
        Assert.Equal("Built-in", beta.Owner);
        Assert.Equal("Global", beta.Scope);
        Assert.Equal("Ctrl+Alt+B, Ctrl+Alt+C", beta.Defaults);
        Assert.Equal("Ctrl+Alt+B, Ctrl+Alt+C", beta.Current);
        Assert.False(beta.IsChanged);
        Assert.Equal("Extension One", Row(page, Gamma).Owner);
    }

    [Fact]
    public void TheFilterMatchesLabelIdAndOwner()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        page.FilterText = "alp";
        Assert.Equal([Alpha], page.VisibleRows.Select(row => row.CommandId));

        page.FilterText = "extension";
        Assert.Equal([Gamma], page.VisibleRows.Select(row => row.CommandId));

        page.FilterText = "";
        Assert.Equal(3, page.VisibleRows.Count);
    }

    [Fact]
    public async Task TheFilterCanBeARecordedGesture()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        page.RecordFilterCommand.Execute(null);
        Assert.True(page.IsRecording);
        await page.CaptureCommand.ExecuteAsync("Ctrl+Alt+C");

        Assert.False(page.IsRecording);
        Assert.Equal([Beta], page.VisibleRows.Select(row => row.CommandId));

        page.ClearFilterGestureCommand.Execute(null);
        Assert.Equal(3, page.VisibleRows.Count);
    }

    [Fact]
    public async Task RecordFirstVisibleRecordsTheFirstFilteredRowAndAnyChangeAsksForTheFilter()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);
        page.FilterText = "Beta";

        page.RecordFirstVisibleCommand.Execute(null);

        Assert.Equal(Beta, page.RecordingRow?.CommandId);

        int before = page.FilterFocusRequest;
        await page.CaptureCommand.ExecuteAsync("Ctrl+Alt+Shift+Q");

        Assert.Equal(before + 1, page.FilterFocusRequest);
    }

    [Fact]
    public async Task AddingAGestureAppliesAtOnceAndIsStoredAsADifference()
    {
        JsonFileSettingsStore store = NewStore();
        ContributionRegistry registry = NewRegistry(store);
        KeyboardShortcutsItemViewModel page = Open(registry, store);

        await Record(page, Alpha, "Ctrl+Alt+X");

        Assert.Equal("Ctrl+Alt+A, Ctrl+Alt+X", Row(page, Alpha).Current);
        Assert.True(Row(page, Alpha).IsChanged);
        Assert.Equal(["Ctrl+Alt+A", "Ctrl+Alt+X"], registry.Commands.Single(command => command.Id == Alpha).DefaultGestures?.Select(gesture => gesture.ToString()));
        KeyBindingOverride entry = Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings);
        Assert.Equal(Alpha, entry.Command);
        Assert.Equal(["Ctrl+Alt+X"], entry.Add);
        Assert.Empty(entry.Remove);
    }

    [Fact]
    public async Task RemovingADefaultGestureStoresARemoveAndRemovingAnAddedOneDropsTheAdd()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        await Row(page, Beta).Gestures.Single(chip => chip.Text == "Ctrl+Alt+B").RemoveCommand.ExecuteAsync(null);

        Assert.Equal("Ctrl+Alt+C", Row(page, Beta).Current);
        Assert.Equal(["Ctrl+Alt+B"], Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings).Remove);

        await Record(page, Beta, "Ctrl+Alt+X");
        await Row(page, Beta).Gestures.Single(chip => chip.Text == "Ctrl+Alt+X").RemoveCommand.ExecuteAsync(null);

        KeyBindingOverride entry = Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings);
        Assert.Empty(entry.Add);
    }

    [Fact]
    public async Task ResetRestoresOneCommandAndResetAllRestoresEveryCommand()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);
        await Record(page, Alpha, "Ctrl+Alt+X");
        await Record(page, Beta, "Ctrl+Alt+Y");

        await Row(page, Alpha).ResetCommand.ExecuteAsync(null);

        Assert.Equal("Ctrl+Alt+A", Row(page, Alpha).Current);
        Assert.Equal("Ctrl+Alt+B, Ctrl+Alt+C, Ctrl+Alt+Y", Row(page, Beta).Current);
        Assert.Equal(Beta, Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings).Command);

        await page.ResetAllCommand.ExecuteAsync(null);

        Assert.Equal("Ctrl+Alt+B, Ctrl+Alt+C", Row(page, Beta).Current);
        Assert.Empty(store.Get(KeyboardSettings.Descriptor).Bindings);
    }

    [Fact]
    public async Task AConflictNamesTheOtherCommandAndCancellingChangesNothing()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        await Record(page, Alpha, "Ctrl+Alt+B");

        GestureConflictViewModel conflict = Assert.IsType<GestureConflictViewModel>(page.Conflict);
        Assert.StartsWith("\"Beta\"", conflict.Message, StringComparison.Ordinal);
        conflict.CancelCommand.Execute(null);

        Assert.Null(page.Conflict);
        Assert.Equal("Ctrl+Alt+A", Row(page, Alpha).Current);
        Assert.Equal("Ctrl+Alt+B, Ctrl+Alt+C", Row(page, Beta).Current);
        Assert.Empty(store.Get(KeyboardSettings.Descriptor).Bindings);
    }

    [Fact]
    public async Task AcceptingAConflictMovesTheGestureFromTheOtherCommand()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);
        await Record(page, Alpha, "Ctrl+Alt+B");

        await Assert.IsType<GestureConflictViewModel>(page.Conflict).AcceptCommand.ExecuteAsync(null);

        Assert.Null(page.Conflict);
        Assert.Equal("Ctrl+Alt+A, Ctrl+Alt+B", Row(page, Alpha).Current);
        Assert.Equal("Ctrl+Alt+C", Row(page, Beta).Current);
        KeyBindingOverride other = store.Get(KeyboardSettings.Descriptor).Bindings.Single(entry => entry.Command == Beta);
        Assert.Equal(["Ctrl+Alt+B"], other.Remove);
    }

    [Fact]
    public async Task AGestureTheScopeRulesRefuseIsExplainedAndNothingChanges()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        await Record(page, Alpha, "K");

        Assert.True(page.HasRefusal);
        Assert.Contains("Global", page.Refusal, StringComparison.Ordinal);
        Assert.Equal("Ctrl+Alt+A", Row(page, Alpha).Current);
        Assert.Empty(store.Get(KeyboardSettings.Descriptor).Bindings);
    }

    [Fact]
    public async Task AnOverrideOfACommandThatIsNotLoadedIsListedAndCanBeRemoved()
    {
        JsonFileSettingsStore store = NewStore();
        await store.SetAsync(
            KeyboardSettings.Descriptor,
            new KeyboardSettings { Bindings = [new KeyBindingOverride { Command = "ext.gone.run", Add = ["Ctrl+Alt+G"] }] },
            TestContext.Current.CancellationToken);
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);

        NotLoadedRowViewModel row = Assert.Single(page.NotLoaded);
        Assert.Contains("ext.gone.run", row.Summary, StringComparison.Ordinal);
        Assert.True(page.HasNotLoaded);

        await row.RemoveCommand.ExecuteAsync(null);

        Assert.Empty(page.NotLoaded);
        Assert.Empty(store.Get(KeyboardSettings.Descriptor).Bindings);
    }

    [Fact]
    public async Task ChangesSurviveARestart()
    {
        JsonFileSettingsStore store = NewStore();
        KeyboardShortcutsItemViewModel page = Open(NewRegistry(store), store);
        await Record(page, Alpha, "Ctrl+Alt+X");
        await Row(page, Beta).Gestures.Single(chip => chip.Text == "Ctrl+Alt+B").RemoveCommand.ExecuteAsync(null);

        JsonFileSettingsStore reopened = NewStore();
        KeyboardShortcutsItemViewModel again = Open(NewRegistry(reopened), reopened);

        Assert.Equal("Ctrl+Alt+A, Ctrl+Alt+X", Row(again, Alpha).Current);
        Assert.Equal("Ctrl+Alt+C", Row(again, Beta).Current);
        Assert.Contains("\"keyboard\"", await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
