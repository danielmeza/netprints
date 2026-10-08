using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.UITests.Commands;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The command palette window: opening, filtering, Enter and Esc (FR-060).</summary>
public class CommandPaletteTests
{
    private sealed class Probe(bool enabled) : ICommandHandler
    {
        public int Runs { get; private set; }

        public bool CanExecute(CommandContext context) => enabled;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) =>
            new(new KeyStubShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (CommandPaletteViewModel Palette, Probe Save, Probe Run) Create()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        var save = new Probe(true);
        var run = new Probe(false);
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "save", "Save", save, DefaultGestures: ["Ctrl+S"], Menu: new MenuPlacement("File", "save", 0)));
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "run", "Run", run, DefaultGestures: ["F5"], Menu: new MenuPlacement("Build", "run", 0)));
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "undo", "Undo", new Probe(true)));
        registry.Freeze();
        return (new CommandPaletteViewModel(registry, new CommandInvoker(registry, new Contexts(), exception => throw exception)), save, run);
    }

    private static string[] Rows(HeadlessUi ui) =>
        [.. ui.Tree.FindControls(new AutomationQuery(AutomationIds.PaletteRow)).Select(pair => Avalonia.Automation.AutomationProperties.GetName(pair.Control) ?? "")];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void OpeningListsEveryCommandIncludingTheDisabledOne()
    {
        var (palette, _, _) = Create();
        using var ui = HeadlessUi.Create();

        ui.Show(new CommandPaletteDialog(palette));

        Assert.Equal(["Run", "Save", "Undo"], Rows(ui));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TypingFiltersAndEnterRunsTheSelectedCommandAndCloses()
    {
        var (palette, save, _) = Create();
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new CommandPaletteDialog(palette));

        await ui.Driver.TypeAsync("sav", Token);
        Assert.Equal(["Save"], Rows(ui));
        await ui.Driver.PressAsync("Enter", Token);

        Assert.Equal(1, save.Runs);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterOnADisabledCommandRunsNothingAndKeepsThePaletteOpen()
    {
        var (palette, _, run) = Create();
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new CommandPaletteDialog(palette));

        await ui.Driver.TypeAsync("run", Token);
        await ui.Driver.PressAsync("Enter", Token);

        Assert.Equal(0, run.Runs);
        Assert.True(dialog.IsVisible);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DownMovesTheSelectionAndEscapeClosesWithoutRunning()
    {
        var (palette, save, _) = Create();
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new CommandPaletteDialog(palette));

        await ui.Driver.PressAsync("Down", Token);
        Assert.Equal("Save", palette.SelectedItem?.Label);
        await ui.Driver.PressAsync("Escape", Token);

        Assert.False(dialog.IsVisible);
        Assert.Equal(0, save.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ATypedQueryWithNoMatchShowsTheEmptyState()
    {
        var (palette, _, _) = Create();
        using var ui = HeadlessUi.Create();
        ui.Show(new CommandPaletteDialog(palette));

        await ui.Driver.TypeAsync("zzz", Token);

        Assert.Empty(Rows(ui));
        Assert.Single(ui.Tree.FindControls(new AutomationQuery(AutomationIds.PaletteEmpty)));
    }
}
