using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Contributions;

/// <summary>US4 scenario 6 and SC-003: one test contribution of each kind appears in its surface like a built-in one (contracts/contributions.md section 4).</summary>
public class TestContributionSurfaceTests
{
    private const string HelloId = "testext.command.hello";
    private const string ShowNotesId = "testext.command.showNotes";
    private const string NotesPanelId = "testext.panel.notes";
    private const string TileBody = "Hello tile body";
    private const string TemplateId = "testext.template.empty";
    private const string GoToKind = "Testext";
    private const string GoToTitle = "Hello result";

    private static readonly string[] MenuLessByDesign = ["openGraph", "cancel"];

    private static readonly DocumentId Doc = DocumentId.Graph("A.netpc.json", DocumentId.ClassGraphKey);

    private sealed class HelloHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public bool CheckAccess() => true;
    }

    private sealed class HelloTooltip : ITooltipProvider
    {
        public int Order => -1;

        public TooltipContent? TryProvide(TooltipTarget target) =>
            target.Subject is string ? new TooltipContent("Hello tip", ["a line"], "some documentation") : null;
    }

    private sealed class HelloGoTo : IGoToProvider
    {
        public string Kind => GoToKind;

        public IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken) =>
            text.Contains("hello", StringComparison.OrdinalIgnoreCase)
                ? new[] { new GoToItem(GoToKind, GoToTitle, "Class", new NavigationTarget(Doc, "node")) }.ToAsyncEnumerable()
                : AsyncEnumerable.Empty<GoToItem>();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void AddTestContributions(IContributionRegistry registry)
    {
        registry.AddCommand(new CommandDescriptor(HelloId, "Say hello", new HelloHandler(), DefaultGestures: ["Ctrl+Alt+H"], Menu: new MenuPlacement("File", "testext", 0)));
        registry.AddPanel(new PanelDescriptor(NotesPanelId, "Notes", _ => new object(), PanelDock.Bottom, 9));
        registry.AddDashboardTile(new DashboardTileDescriptor("testext.tile.hello", "Hello", 99, _ => TileBody));
        registry.AddProjectTemplate(new ProjectTemplateDescriptor(TemplateId, "Empty test app", "An empty template", DefaultProjectProfile.ProfileId, ProjectOutputType.Library));
        registry.AddContextMenuItem(new ContextMenuItemDescriptor("testext.menu.hello", ContextMenuTarget.Connection, HelloId, "testext", 99));
        registry.AddTooltipProvider(new HelloTooltip());
        registry.AddGoToProvider(new HelloGoTo());
    }

    private static ContributionRegistry NewRegistry(bool withTestContributions)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        if (withTestContributions)
        {
            AddTestContributions(registry);
        }

        registry.Freeze();
        return registry;
    }

    private static string[] Names(HeadlessUi ui, string automationId) =>
        [.. ui.Tree.FindControls(new AutomationQuery(automationId)).Select(pair => Avalonia.Automation.AutomationProperties.GetName(pair.Control) ?? "")];

    private static string ShortcutOf(MenuItem item) =>
        item.GetVisualDescendants().OfType<TextBlock>().Where(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == AutomationIds.ShellMenuShortcut).Select(text => text.Text ?? "").SingleOrDefault() ?? "";

    private static MenuItem Item(IEnumerable<MenuItem> items, string commandId) =>
        items.Single(item => Avalonia.Automation.AutomationProperties.GetAutomationId(item) == AutomationIds.MenuPrefix + commandId);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestCommandIsInTheMenuAndShowsItsShortcutLikeABuiltInOne()
    {
        using var rig = SurfaceRig.Create(AddTestContributions);

        IReadOnlyList<MenuItem> file = rig.Open("File");
        MenuItem hello = Item(file, HelloId);
        MenuItem save = Item(file, ContributionIds.CommandPrefix + "save");

        Assert.Equal("Say hello", Assert.IsType<CommandEntryViewModel>(hello.Header).Label);
        Assert.Equal("Ctrl+Alt+H", ShortcutOf(hello));
        Assert.Equal(save.IsEnabled, hello.IsEnabled);
        Assert.True(file.ToList().IndexOf(hello) > file.ToList().IndexOf(save));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestCommandIsInThePaletteAndTheShortcutsSheetLikeABuiltInOne()
    {
        using var rig = SurfaceRig.Create(AddTestContributions);
        var palette = new CommandPaletteViewModel(rig.Registry, rig.Invoker);
        var sheet = new KeyboardShortcutsViewModel(rig.Registry);
        palette.Query = "hello";
        using var paletteUi = HeadlessUi.Create();
        using var sheetUi = HeadlessUi.Create();

        paletteUi.Show(new CommandPaletteDialog(palette));
        sheetUi.Show(new KeyboardShortcutsDialog(sheet));

        Assert.Equal(["Say hello"], Names(paletteUi, AutomationIds.PaletteRow));
        palette.Query = "";
        Assert.Equal(rig.Registry.Commands.Count, palette.Items.Count);
        Assert.Contains(palette.Items, candidate => candidate.Label == "Save");
        PaletteItem item = palette.Items.Single(candidate => candidate.Command.Id == HelloId);
        Assert.Equal(("Ctrl+Alt+H", "File", true), (item.Shortcuts, item.MenuPath, item.IsEnabled));
        Assert.Equal(rig.Registry.Commands.Count, Names(sheetUi, AutomationIds.ShortcutsRow).Length);
        ShortcutGroup file = sheet.Groups.Single(group => group.Title == "File");
        Assert.Equal(new ShortcutRow(HelloId, "Say hello", "Ctrl+Alt+H"), file.Rows.Single(row => row.CommandId == HelloId));
        Assert.True(file.Rows.Select(row => row.CommandId).ToList().IndexOf(HelloId) > file.Rows.Select(row => row.CommandId).ToList().IndexOf(ContributionIds.CommandPrefix + "save"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestPanelIsInTheViewMenuAndTheLayoutAndItsShowCommandBringsItBack()
    {
        using var rig = SurfaceRig.Create(AddTestContributions);

        IReadOnlyList<MenuItem> view = rig.Open("View");
        string[] panelEntries = [.. view.Select(item => Avalonia.Automation.AutomationProperties.GetAutomationId(item) ?? "").Where(id => id.Contains("showPanel", StringComparison.Ordinal) || id.Contains(NotesPanelId, StringComparison.Ordinal))];

        Assert.Equal(7, panelEntries.Length);
        Assert.Contains(rig.Shell.Panels, panel => panel.Id == NotesPanelId && panel.Title == "Notes");
        Assert.True(rig.Adapter.IsPanelVisible(NotesPanelId));
        Assert.True(rig.Adapter.IsPanelVisible(PanelContributions.ErrorsId));
        rig.Adapter.HidePanel(NotesPanelId);
        Assert.False(rig.Adapter.IsPanelVisible(NotesPanelId));
        MenuItem entry = view.Single(item => (Avalonia.Automation.AutomationProperties.GetAutomationId(item) ?? "").Contains(NotesPanelId, StringComparison.Ordinal));
        Assert.Equal("Notes", Assert.IsType<CommandEntryViewModel>(entry.Header).Label);
        entry.Command?.Execute(entry.CommandParameter);
        rig.Settle();
        Assert.True(rig.Adapter.IsPanelVisible(NotesPanelId));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestTileIsOnTheStartPageAfterTheBuiltInTiles()
    {
        ContributionRegistry registry = NewRegistry(withTestContributions: true);
        var shell = new ShellViewModel(registry, new StartPageServices(), TimeProvider.System, new ImmediateDispatcher());
        var services = new StartPageServices().Add<IProjectActions>(new NoProjectActions());
        using var page = new StartPageViewModel(shell, services);
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new Window { Width = 1600, Height = 1000, Content = new StartPageView { DataContext = page } });

        Assert.Equal(TileBody, page.Tiles[^1]);
        Assert.Equal(registry.DashboardTiles.Count, page.Tiles.Count);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == TileBody);
        shell.Dispose();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestTemplateIsInTheNewProjectDialogAfterTheBuiltInOnes()
    {
        ContributionRegistry registry = NewRegistry(withTestContributions: true);
        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        var service = new ProjectTemplateService(() => registry.ProjectTemplates, _ => DefaultProjectProfile.Instance, projects);
        using var ui = HeadlessUi.Create();

        ui.Show(new NewProjectDialog(new NewProjectDialogViewModel(service, new QueuedFilePicker(), new ProjectLocations(null, Path.GetTempPath()), TimeProvider.System)));

        ListBox templates = Assert.IsType<ListBox>(ui.Tree.FindControls(new AutomationQuery(AutomationIds.NewProjectTemplates)).Select(pair => pair.Control).Single());
        string[] shown = [.. templates.GetLogicalDescendants().OfType<ListBoxItem>().Select(item => Assert.IsType<ProjectTemplateDescriptor>(item.DataContext).Id)];
        Assert.Equal([ContributionIds.TemplatePrefix + "console", ContributionIds.TemplatePrefix + "library", TemplateId], shown);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestContextMenuItemIsOfferedAfterTheBuiltInOnesOfItsTarget()
    {
        using var rig = SurfaceRig.Create(AddTestContributions);

        string[] connection = [.. rig.Invoker.ContextMenuCommands(ContextMenuTarget.Connection).Select(command => command.Id)];
        string[] canvas = [.. rig.Invoker.ContextMenuCommands(ContextMenuTarget.Canvas).Select(command => command.Id)];

        Assert.Equal([ContributionIds.CommandPrefix + "goToSource", ContributionIds.CommandPrefix + "goToTarget", HelloId], connection);
        Assert.DoesNotContain(HelloId, canvas);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ATestTooltipProviderIsAskedInOrderAndTheFirstNonNullContentWins()
    {
        using var rig = SurfaceRig.Create(AddTestContributions);

        TooltipContent? mine = rig.Invoker.TooltipFor(new TooltipTarget(TooltipTargetKind.Connection, "any"));
        TooltipContent? nobody = rig.Invoker.TooltipFor(new TooltipTarget(TooltipTargetKind.Connection, new object()));

        Assert.Equal(new TooltipContent("Hello tip", ["a line"], "some documentation").Title, mine?.Title);
        Assert.Null(nobody);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ATestGoToProviderShowsItsResultsUnderItsKindAndTheCommandsProviderListsTheTestCommand()
    {
        var live = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(live);
        GoToProviderContributions.Register(live, () => null);
        AddTestContributions(live);
        live.Freeze();
        List<NavigationTarget> navigated = [];
        var goTo = new GoToAnythingViewModel(live.GoToProviders, target =>
        {
            navigated.Add(target);
            return true;
        }, _ => true);
        using var ui = HeadlessUi.Create();
        ui.Show(new GoToAnythingDialog(goTo));

        await ui.Driver.TypeAsync("hello", Token);
        await Task.Yield();

        Assert.Contains(GoToKind, Names(ui, AutomationIds.GoToHeader));
        Assert.Contains(GoToTitle, Names(ui, AutomationIds.GoToRow));
        await goTo.SearchCommand.ExecuteAsync(null);
        GoToRow row = goTo.Rows.Single(candidate => candidate is { IsHeader: false } && candidate.Title == GoToTitle);
        Assert.Equal(GoToKind, row.Item?.Kind);
        goTo.Query = ">hello";
        await goTo.SearchCommand.ExecuteAsync(null);
        Assert.Contains(goTo.Rows, candidate => candidate.Item?.CommandId == HelloId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryBuiltInCommandIsInAMenuUnlessMenuLessByDesignAndInThePalette()
    {
        using var rig = SurfaceRig.Create();
        var palette = new CommandPaletteViewModel(rig.Registry, rig.Invoker);
        MenuBarViewModel menuBar = Assert.IsType<MenuBarViewModel>(rig.Shell.MenuBar);

        string[] inMenus = [.. menuBar.Menus.SelectMany(menu => menu.Items).Where(entry => !entry.IsSeparator).Select(entry => entry.Id)];
        string[] inPalette = [.. palette.Items.Select(item => item.Command.Id)];
        string[] menuLess = [.. rig.Registry.Commands.Where(command => command.Menu is null).Select(command => command.Id[ContributionIds.CommandPrefix.Length..])];

        Assert.Equal(MenuLessByDesign.Order(StringComparer.Ordinal), menuLess.Order(StringComparer.Ordinal));
        Assert.All(rig.Registry.Commands.Where(command => command.Menu is not null), command => Assert.Contains(command.Id, inMenus));
        Assert.Equal(rig.Registry.Commands.Select(command => command.Id).Order(StringComparer.Ordinal), inPalette.Order(StringComparer.Ordinal));
    }
}
