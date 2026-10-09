using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Commands;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Controls;

/// <summary>The empty-state control stands in for the blank space of every panel and list that has nothing to show (FR-088).</summary>
public class EmptyStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Probe : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
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

    private static EmptyState ShownOne(Visual root, string automationId)
    {
        var shown = root.GetVisualDescendants().OfType<EmptyState>()
            .Where(state => state.IsEffectivelyVisible && AutomationProperties.GetAutomationId(state) == automationId)
            .ToList();
        EmptyState state = Assert.Single(shown);
        Assert.False(string.IsNullOrWhiteSpace(state.Message), $"{automationId} has a sentence");
        Assert.False(string.IsNullOrEmpty(state.IconId), $"{automationId} has an icon id");
        return state;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            HeadlessDriver.Pump();
            await Task.Delay(50, Token);
        }

        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ErrorsWithNoDiagnosticsShowOne()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        EmptyState state = ShownOne(session.Window, AutomationIds.ErrorsClean);

        Assert.Equal(IconIds.EmptyErrors, state.IconId);
        Assert.Null(state.ActionCommand);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OutputWithNoLinesShowsOne()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        session.App.Api.ShowPanel(PanelContributions.OutputId);
        HeadlessDriver.Pump();

        EmptyState state = ShownOne(session.Window, AutomationIds.OutputEmpty);

        Assert.Equal(IconIds.EmptyOutput, state.IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheInspectorWithNoSelectionShowsOne()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = Assert.IsType<ProjectTreePanelViewModel>(session.App.Shell.FindPanel(PanelContributions.ProjectTreeId)?.Content);

        tree.SelectedItem = null;
        HeadlessDriver.Pump();

        EmptyState state = ShownOne(session.Window, AutomationIds.InspectorEmpty);
        Assert.Equal(IconIds.EmptyInspector, state.IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodeSearchWithNoResultsShowsOne()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);

        await session.Driver.TypeAsync("zzzzqqqq", Token);
        await WaitAsync(() => session.Window.GetVisualDescendants().OfType<EmptyState>()
            .Any(state => state.IsEffectivelyVisible && AutomationProperties.GetAutomationId(state) == AutomationIds.SearchEmpty));

        EmptyState state = ShownOne(session.Window, AutomationIds.SearchEmpty);
        Assert.Equal(IconIds.EmptySearch, state.IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePaletteWithNoResultsShowsOne()
    {
        using var ui = HeadlessUi.Create();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "save", "Save", new Probe()));
        registry.Freeze();
        var palette = new CommandPaletteViewModel(registry, new CommandInvoker(registry, new Contexts(), exception => throw exception));
        var dialog = ui.Show(new CommandPaletteDialog(palette));

        palette.Query = "zzzzqqqq";
        HeadlessDriver.Pump();

        Assert.Equal(IconIds.EmptySearch, ShownOne(dialog, AutomationIds.PaletteEmpty).IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task GoToAnythingWithNoResultsShowsOne()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new GoToAnythingDialog(new GoToAnythingViewModel([], _ => true, _ => true)));

        await ui.Driver.TypeAsync("zzz", Token);

        Assert.Equal(IconIds.EmptySearch, ShownOne(dialog, AutomationIds.GoToEmpty).IconId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheProjectTreeWithNoProjectShowsOneWhoseActionOpensAProject()
    {
        await using var app = ShellApp.Start();
        var tree = Assert.IsType<ProjectTreePanelViewModel>(app.Shell.FindPanel(PanelContributions.ProjectTreeId)?.Content);
        var window = app.Ui.Show(new Window { Width = 400, Height = 400, Content = new ProjectTreePanelView { DataContext = tree } });

        EmptyState state = ShownOne(window, AutomationIds.TreeEmpty);

        Assert.Equal(IconIds.EmptyProject, state.IconId);
        Assert.Equal("Open project…", state.ActionText);
        Assert.NotNull(state.ActionCommand);
        Assert.Empty(app.FilePicker.Requests);

        state.ActionCommand.Execute(null);
        await WaitAsync(() => app.FilePicker.Requests.Count == 1);

        Assert.Single(app.FilePicker.Requests);
    }
}
