using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Material.Icons.Avalonia;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Projects;
using NetPrints.Testing.Ui.Driving;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The menus, the command bar and the status bar are generated from the registry (contracts/contributions.md sections 3 and 4, commands.md section 1).</summary>
public class RegistrySurfaceTests
{
    private const string Prefix = ContributionIds.CommandPrefix;
    private const string Separator = "-";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool IsSeparator(MenuItem item) => !item.Focusable;

    private static string[] Layout(IEnumerable<MenuItem> items) =>
        [.. items.Select(item => IsSeparator(item) ? Separator : Assert.IsType<string>(AutomationProperties_Id(item)))];

    private static string? AutomationProperties_Id(Control control) => Avalonia.Automation.AutomationProperties.GetAutomationId(control);

    private static string[] Ids(params string[] names) => [.. names.Select(name => name == Separator ? name : Prefix + name)];

    private static object? TipOf(SurfaceRig rig, string automationId) => ToolTip.GetTip(Assert.IsAssignableFrom<Control>(rig.Find(automationId)));

    private static string IdOf(Control control) => Avalonia.Automation.AutomationProperties.GetAutomationId(control) ?? "";

    private static string MenuId(string commandId) => AutomationIds.MenuPrefix + commandId;

    private static string BarId(string commandId) => AutomationIds.CommandBarPrefix + commandId;

    private static async Task<(HeadlessApp App, SampleCopy Sample, ProjectSessionViewModel Session)> OpenProjectAsync()
    {
        var app = HeadlessApp.Start();
        var sample = new SampleCopy();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        return (app, sample, Assert.IsType<ProjectSessionViewModel>(app.Session));
    }

    private static DocumentId DocumentOf(ProjectSessionViewModel session) => DocumentId.Graph(session.ClassPathOf(session.Project.Classes.Single()), "class");

    private static void OpenDocument(SurfaceRig rig, ProjectSessionViewModel session)
    {
        var document = new TestDocumentViewModel(DocumentOf(session), "Program");
        rig.Shell.AddDocument(document);
        rig.Shell.ActiveDocument = document;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheMenusComeInTheContractOrderWithTheirAutomationIds()
    {
        using var rig = SurfaceRig.Create();

        Assert.Equal(["File", "Edit", "View", "Go", "Build", "Help"], rig.TopMenus().Select(menu => Assert.IsType<string>(menu.Header)));
        Assert.Equal(["File", "Edit", "View", "Go", "Build", "Help"], rig.TopMenus().Select(menu => AutomationProperties_Id(menu)?[AutomationIds.MenuPrefix.Length..]));
        Assert.NotNull(rig.Find(AutomationIds.ShellMenuBar));
        Assert.NotNull(rig.Find(AutomationIds.ShellCommandBar));
        Assert.NotNull(rig.Find(AutomationIds.ShellStatusBar));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryMenuListsItsCommandsInGroupOrderSeparatedByDividers()
    {
        using var rig = SurfaceRig.Create();

        Assert.Equal(Ids("newProject", "openProject", "closeProject", Separator, "newClass", "addExistingClass", Separator, "save", "saveAll", Separator, "projectSettings", "references", Separator, "exit"), Layout(rig.Open("File")).Select(Strip(MenuId)));
        Assert.Equal(Ids("undo", "redo", Separator, "delete", "rename", "selectAll", Separator, "nodeSearch", Separator, "classSettings", "addMethod", "addConstructor", "addVariable", "addEventGraph", "overrideMethod"), Layout(rig.Open("Edit")).Select(Strip(MenuId)));
        Assert.Equal(
            Ids("frameSelection", "fitAll", Separator, "showPanel.projectTree", "showPanel.inspector", "showPanel.variables", "showPanel.errors", "showPanel.output", "showPanel.csharp", Separator, "floatDocument", "dockDocument", "resetLayout"),
            Layout(rig.Open("View")).Select(Strip(MenuId)));
        Assert.Equal(Ids("compile", Separator, "run", "stop"), Layout(rig.Open("Build")).Select(Strip(MenuId)));
        Assert.Equal(Ids("nextTab", "previousTab", "closeTab"), Layout(rig.Open("Go")).Select(Strip(MenuId)));
        Assert.Empty(rig.Open("Help"));
    }

    private static Func<string, string> Strip(Func<string, string> prefixOf) => id => id == Separator ? id : id.StartsWith(prefixOf(""), StringComparison.Ordinal) ? id[prefixOf("").Length..] : id;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryCommandWithAMenuIsInTheMenuBarAndTheMenuLessOnesAreNot()
    {
        using var rig = SurfaceRig.Create();

        MenuBarViewModel menuBar = Assert.IsType<MenuBarViewModel>(rig.Shell.MenuBar);
        string[] inMenus = [.. menuBar.Menus.SelectMany(menu => menu.Items).Where(entry => !entry.IsSeparator).Select(entry => entry.Id)];
        string[] withMenu = [.. rig.Registry.Commands.Where(command => command.Menu is not null).Select(command => command.Id)];

        Assert.Equal(withMenu.Order(StringComparer.Ordinal), inMenus.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(Prefix + "cancel", inMenus);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void MenuItemsShowTheirFirstShortcutAsWrittenAndNoneWhenTheyHaveNo()
    {
        using var rig = SurfaceRig.Create();
        IReadOnlyList<MenuItem> edit = rig.Open("Edit");
        IReadOnlyList<MenuItem> build = rig.Open("Build");

        Assert.Equal("Ctrl+Z", ShortcutOf(Item(edit, "undo")));
        Assert.Equal("Ctrl+Y", ShortcutOf(Item(edit, "redo")));
        Assert.Equal("", ShortcutOf(Item(edit, "addMethod")));
        Assert.Equal("F7", ShortcutOf(Item(build, "compile")));
        Assert.Equal("Shift+F5", ShortcutOf(Item(build, "stop")));
    }

    private static MenuItem Item(IEnumerable<MenuItem> items, string commandName) =>
        items.Single(item => AutomationProperties_Id(item) == MenuId(Prefix + commandName));

    private static string HeaderOf(MenuItem item) => item.GetVisualDescendants().OfType<TextBlock>().First().Text ?? "";

    private static string ShortcutOf(MenuItem item) =>
        item.GetVisualDescendants().OfType<TextBlock>().Where(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == AutomationIds.ShellMenuShortcut).Select(text => text.Text ?? "").SingleOrDefault() ?? "";

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ACommandThatCannotRunIsDisabledAndComesBackWithAProject()
    {
        using var rig = SurfaceRig.Create();
        IReadOnlyList<MenuItem> build = rig.Open("Build");
        IReadOnlyList<MenuItem> file = rig.Open("File");

        Assert.False(Item(build, "compile").IsEffectivelyEnabled);
        Assert.False(Item(build, "run").IsEffectivelyEnabled);
        Assert.False(Item(build, "stop").IsEffectivelyEnabled);
        Assert.False(Item(file, "save").IsEffectivelyEnabled);
        Assert.True(Item(file, "newProject").IsEffectivelyEnabled);
        Assert.True(Item(file, "openProject").IsEffectivelyEnabled);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAProjectEnablesItsCommandsOnTheNextQuery()
    {
        var (app, sample, session) = await OpenProjectAsync();
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create())
        {
            IReadOnlyList<MenuItem> build = rig.Open("Build");
            Assert.False(Item(build, "compile").IsEffectivelyEnabled);

            rig.Shell.Session = session;
            rig.Settle();

            Assert.True(Item(build, "compile").IsEffectivelyEnabled);
            Assert.True(Item(build, "run").IsEffectivelyEnabled);
            Assert.False(Item(build, "stop").IsEffectivelyEnabled);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task UndoShowsItsActionAndIsEnabledAfterAnEditThenRedoTakesItsPlace()
    {
        var (app, sample, session) = await OpenProjectAsync();
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create(session: session))
        {
            OpenDocument(rig, session);
            IReadOnlyList<MenuItem> edit = rig.Open("Edit");
            MenuItem undo = Item(edit, "undo");
            MenuItem redo = Item(edit, "redo");
            Assert.Equal("Undo", HeaderOf(undo));
            Assert.False(undo.IsEffectivelyEnabled);

            ClassGraph cls = session.Project.Classes.Single();
            session.UndoStackFor(cls).Do(new DelegateUndoableCommand("Add node", () => { }, () => { }));
            rig.Settle();

            Assert.Equal("Undo Add node", HeaderOf(undo));
            Assert.True(undo.IsEffectivelyEnabled);
            Assert.False(redo.IsEffectivelyEnabled);
            Assert.Equal("Undo Add node (Ctrl+Z)", ToolTip.GetTip(Assert.IsType<Button>(rig.Find(BarId(Prefix + "undo")))));

            session.UndoStackFor(cls).Undo();
            rig.Settle();

            Assert.Equal("Undo", HeaderOf(undo));
            Assert.False(undo.IsEffectivelyEnabled);
            Assert.Equal("Redo Add node", HeaderOf(redo));
            Assert.True(redo.IsEffectivelyEnabled);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheEditMenuAndBarNameTheNodeActionsOfTheCanvas()
    {
        var (app, sample, session) = await OpenProjectAsync();
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create(session: session))
        {
            ClassGraph cls = session.Project.Classes.Single();
            ClassContext classContext = session.ContextFor(cls);
            MethodGraph method = classContext.CreateMethod();
            using var graph = new NodeGraphViewModel(method, classContext.Services);
            var document = new GraphDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), "method:1"), graph, cls, session);
            rig.Shell.AddDocument(document);
            rig.Shell.ActiveDocument = document;
            IReadOnlyList<MenuItem> edit = rig.Open("Edit");
            MenuItem undo = Item(edit, "undo");
            session.UndoStackFor(cls).Clear();
            rig.Settle();
            Assert.Equal("Undo", HeaderOf(undo));
            Assert.False(undo.IsEffectivelyEnabled);

            await graph.OpenSearchAsync(new GraphPoint(100, 100), null, Token);
            await graph.Search.SelectCommand.ExecuteAsync(graph.Search.AllSuggestions.First(item => item.Text == "If Else"));
            rig.Settle();

            Assert.Equal("Undo Add node", HeaderOf(undo));
            Assert.True(undo.IsEffectivelyEnabled);
            Assert.Equal("Undo Add node (Ctrl+Z)", ToolTip.GetTip(Assert.IsType<Button>(rig.Find(BarId(Prefix + "undo")))));

            NodeViewModel node = graph.Nodes.Single(vm => vm.Node is IfElseNode);
            graph.SelectNodes([node], deselectPrevious: true);
            rig.Settle();
            MenuItem delete = Item(edit, "delete");
            Assert.True(delete.IsEffectivelyEnabled);
            delete.Command?.Execute(delete.CommandParameter);
            rig.Settle();

            Assert.Equal("Undo Delete node", HeaderOf(undo));
            Assert.DoesNotContain(graph.Nodes, vm => vm.Node is IfElseNode);

            undo.Command?.Execute(undo.CommandParameter);
            rig.Settle();

            Assert.Contains(graph.Nodes, vm => vm.Node is IfElseNode);
            Assert.Equal("Undo Add node", HeaderOf(undo));
            Assert.Equal("Redo Delete node", HeaderOf(Item(edit, "redo")));
            Assert.Equal("Undid: Delete node", rig.Shell.StatusMessage);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RunAndCompileAreDisabledDuringACompileAndEnabledAgainAfterIt()
    {
        var (app, sample, session) = await OpenProjectAsync();
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create(session: session))
        {
            IReadOnlyList<MenuItem> build = rig.Open("Build");
            Button compileButton = Assert.IsType<Button>(rig.Find(BarId(Prefix + "compile")));
            Button runButton = Assert.IsType<Button>(rig.Find(BarId(Prefix + "run")));
            Assert.True(Item(build, "compile").IsEffectivelyEnabled);
            Assert.True(runButton.IsEffectivelyEnabled);

            Task<bool> compile = session.CompileAsync();
            rig.Settle();

            Assert.False(compile.IsCompleted);
            Assert.False(Item(build, "compile").IsEffectivelyEnabled);
            Assert.False(Item(build, "run").IsEffectivelyEnabled);
            Assert.False(compileButton.IsEffectivelyEnabled);
            Assert.False(runButton.IsEffectivelyEnabled);
            Assert.Equal("Building…", rig.Shell.StatusBar.BuildStateText);

            await compile.WaitAsync(TimeSpan.FromSeconds(120), Token);
            rig.Settle();

            Assert.True(Item(build, "compile").IsEffectivelyEnabled);
            Assert.True(Item(build, "run").IsEffectivelyEnabled);
            Assert.True(compileButton.IsEffectivelyEnabled);
            Assert.True(runButton.IsEffectivelyEnabled);
            Assert.Equal("", rig.Shell.StatusBar.BuildStateText);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheCommandBarIsAtMost40PixelsHighAndShowsItsCommandsInOrderWithAnIconAndALabel()
    {
        using var rig = SurfaceRig.Create();
        var bar = Assert.Single(rig.Ui.Tree.Find(SurfaceRig.Id(AutomationIds.ShellCommandBar)));

        Assert.InRange(bar.Bounds.Height, 1, 40);
        Button[] buttons = [.. rig.FindAll(AutomationIds.CommandBarPrefix + Prefix + "save").Concat(rig.FindAll(BarId(Prefix + "compile"))).Concat(rig.FindAll(BarId(Prefix + "run"))).Concat(rig.FindAll(BarId(Prefix + "undo"))).Concat(rig.FindAll(BarId(Prefix + "redo"))).Concat(rig.FindAll(BarId(Prefix + "classSettings"))).Concat(rig.FindAll(BarId(Prefix + "references"))).OfType<Button>()];
        Assert.Equal(7, buttons.Length);
        Assert.Equal(buttons.Select(IdOf), rig.Ui.Tree.Windows[0].GetVisualDescendants().OfType<Button>().Select(IdOf).Where(id => id.StartsWith(AutomationIds.CommandBarPrefix, StringComparison.Ordinal)));
        Assert.All(buttons, button =>
        {
            Assert.Single(button.GetVisualDescendants().OfType<MaterialIcon>());
            Assert.Contains(button.GetVisualDescendants().OfType<TextBlock>(), text => !string.IsNullOrEmpty(text.Text));
        });
        Assert.Equal(["Save", "Compile", "Run", "Undo", "Redo", "Class settings", "References…"], buttons.Select(button => LabelOf(button)));
        Assert.Empty(rig.FindAll(BarId(Prefix + "stop")));
    }

    private static string LabelOf(Button button) => button.GetVisualDescendants().OfType<TextBlock>().First(text => !string.IsNullOrEmpty(text.Text) && Avalonia.Automation.AutomationProperties.GetAutomationId(text) != AutomationIds.ShellCompileBadge).Text ?? "";

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EachButtonsTooltipIsItsLabelAndFirstShortcut()
    {
        using var rig = SurfaceRig.Create();

        Assert.Equal("Save (Ctrl+S)", TipOf(rig, BarId(Prefix + "save")));
        Assert.Equal("Compile (F7)", TipOf(rig, BarId(Prefix + "compile")));
        Assert.Equal("Run (F5)", TipOf(rig, BarId(Prefix + "run")));
        Assert.Equal("Redo (Ctrl+Y)", TipOf(rig, BarId(Prefix + "redo")));
        Assert.Equal("Class settings", TipOf(rig, BarId(Prefix + "classSettings")));
    }

    private sealed class ControlledLauncher : IProcessLauncher
    {
        public event Action<string>? OutputReceived
        {
            add { }
            remove { }
        }

        public event Action<int, ProcessStartRequest>? ProcessStarted;

        public event Action<int, ProcessStream, string>? LineReceived
        {
            add { }
            remove { }
        }

        public event Action<int, int>? ProcessExited;

        public void Start(ProcessStartRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Starts(ProcessStartRequest request) => ProcessStarted?.Invoke(1, request);

        public void Exits(int code) => ProcessExited?.Invoke(1, code);

    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RunAndStopShareOneSlotAndItShowsStopWhileTheProgramRuns()
    {
        var (app, sample, opened) = await OpenProjectAsync();
        var launcher = new ControlledLauncher();
        using var runState = new RunStateTracker(launcher);
        using var session = new ProjectSessionViewModel(opened.Project, app.Composition.Context with { Processes = launcher, RunState = runState });
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create(session: session))
        {
            Assert.NotNull(rig.Find(BarId(Prefix + "run")));
            Assert.Null(rig.Find(BarId(Prefix + "stop")));

            launcher.Starts(new ProcessStartRequest("dotnet", [], sample.Directory));
            rig.Settle();

            Assert.Null(rig.Find(BarId(Prefix + "run")));
            Button stop = Assert.IsType<Button>(rig.Find(BarId(Prefix + "stop")));
            Assert.True(stop.IsEffectivelyEnabled);
            Assert.Equal("Stop (Shift+F5)", ToolTip.GetTip(stop));
            Assert.Equal("Stop", LabelOf(stop));
            Assert.Equal("Running", rig.Shell.StatusBar.BuildStateText);

            launcher.Exits(0);
            rig.Settle();

            Assert.NotNull(rig.Find(BarId(Prefix + "run")));
            Assert.Null(rig.Find(BarId(Prefix + "stop")));
            Assert.Equal("", rig.Shell.StatusBar.BuildStateText);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CompileShowsAnErrorBadgeAfterACompileWithErrors()
    {
        var (app, sample, session) = await OpenProjectAsync();
        await using (app)
        using (sample)
        using (var rig = SurfaceRig.Create(session: session))
        {
            Assert.Null(rig.Find(AutomationIds.ShellCompileBadge));

            session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(
            [
                new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1002", "; expected", "Program", null, null, null, null),
                new CodeDiagnostic(CodeDiagnosticSeverity.Warning, "CS0168", "unused", "Program", null, null, null, null),
                new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1003", "syntax error", "Program", null, null, null, null),
            ]);
            rig.Settle();

            TextBlock badge = Assert.IsType<TextBlock>(rig.Find(AutomationIds.ShellCompileBadge));
            Assert.Equal("2", badge.Text);
            Assert.Equal("2 errors", Avalonia.Automation.AutomationProperties.GetName(badge));
            Assert.Same(rig.Find(BarId(Prefix + "compile")), badge.FindAncestorOfType<Button>());

            session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>();
            rig.Settle();

            Assert.Null(rig.Find(AutomationIds.ShellCompileBadge));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryIconOnlyElementHasAnAccessibleNameAndEveryInteractiveElementIsReachableWithTheKeyboard()
    {
        using var rig = SurfaceRig.Create();
        MenuItem[] topMenus = [.. rig.TopMenus()];
        IReadOnlyList<MenuItem> file = rig.Open("File");

        Assert.All(topMenus, menu => Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(menu)), menu.Header?.ToString()));
        Assert.All(file.Where(item => !IsSeparator(item)), item => Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(item))));
        Assert.All(file.Where(item => IsSeparator(item)), item => Assert.False(item.Focusable));

        Button[] buttons = [.. rig.FindAll(AutomationIds.CommandBarPrefix + Prefix + "save").OfType<Button>()];
        Assert.NotEmpty(buttons);
        foreach (Button button in rig.Ui.Tree.Windows[0].GetVisualDescendants().OfType<Button>().Where(button => IdOf(button).StartsWith(AutomationIds.CommandBarPrefix, StringComparison.Ordinal)))
        {
            Assert.True(button.Focusable, Avalonia.Automation.AutomationProperties.GetAutomationId(button));
            Assert.True(button.IsTabStop);
            Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(button)) && button.Content is null);
        }

        Assert.All(rig.Window.GetVisualDescendants().OfType<MaterialIcon>(), icon => Assert.Equal(Avalonia.Automation.AccessibilityView.Raw, Avalonia.Automation.AutomationProperties.GetAccessibilityView(icon)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TabMovesFocusThroughTheEnabledCommandBarButtonsInOrder()
    {
        var handler = new AlwaysEnabled();
        using var rig = SurfaceRig.Create(registry =>
        {
            registry.AddCommand(new CommandDescriptor("acme.command.one", "One", handler, "Numeric1", null, CommandScope.Global, null, 20));
            registry.AddCommand(new CommandDescriptor("acme.command.two", "Two", handler, "Numeric2", null, CommandScope.Global, null, 21));
            registry.AddCommand(new CommandDescriptor("acme.command.three", "Three", handler, "Numeric3", null, CommandScope.Global, null, 22));
        });
        Button one = Assert.IsType<Button>(rig.Find(BarId("acme.command.one")));
        Assert.True(one.Focusable && one.IsTabStop);
        one.Focus();
        rig.Settle();
        List<string> visited = [];
        IFocusManager? focus = TopLevel.GetTopLevel(rig.Window)?.FocusManager;

        for (int step = 0; step < 3; step++)
        {
            visited.Add(focus?.GetFocusedElement() is Control control ? IdOf(control) : "");
            await rig.Ui.Driver.PressAsync("Tab", Token);
        }

        Assert.Equal([BarId("acme.command.one"), BarId("acme.command.two"), BarId("acme.command.three")], visited);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ACommandFromAnotherContributionAppearsInItsMenuAndOnTheCommandBar()
    {
        var handler = new AlwaysEnabled();
        using var rig = SurfaceRig.Create(registry =>
        {
            registry.AddCommand(new CommandDescriptor("acme.command.hello", "Say hello", handler, "Hand", ["Ctrl+Alt+H"], CommandScope.Global, new MenuPlacement("View", "acme", 0), 8));
            registry.AddCommand(new CommandDescriptor("acme.command.report", "Report", handler, null, null, CommandScope.Global, new MenuPlacement("Reports", "reports", 0), null));
        });

        Assert.Equal(["File", "Edit", "View", "Go", "Build", "Help", "Reports"], rig.TopMenus().Select(menu => Assert.IsType<string>(menu.Header)));
        IReadOnlyList<MenuItem> view = rig.Open("View");
        Assert.Equal([MenuId(Prefix + "resetLayout"), Separator, MenuId("acme.command.hello")], Layout(view).TakeLast(3));
        Assert.Equal("Ctrl+Alt+H", ShortcutOf(view[^1]));
        Button hello = Assert.IsType<Button>(rig.Find(BarId("acme.command.hello")));
        Assert.Equal("Say hello (Ctrl+Alt+H)", ToolTip.GetTip(hello));
        Assert.Equal("Say hello", LabelOf(hello));

        Assert.True(rig.Invoker.TryRun(rig.Registry.Commands.Single(command => command.Id == "acme.command.hello")));
        Assert.Equal(1, handler.Runs);
    }

    private sealed class AlwaysEnabled : ICommandHandler
    {
        public int Runs { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheStatusBarShowsTheMessageAndTheBuildState()
    {
        using var rig = SurfaceRig.Create();
        TextBlock message = Assert.IsType<TextBlock>(rig.Find(AutomationIds.ShellStatusMessage));
        Assert.True(string.IsNullOrEmpty(message.Text));

        rig.Shell.ShowStatus("Saved 2 files");
        rig.Settle();
        Assert.Equal("Saved 2 files", message.Text);

        rig.Shell.StatusBar.SetBuildState(BuildState.Building);
        rig.Settle();
        Assert.Equal("Building…", Assert.IsType<TextBlock>(rig.Find(AutomationIds.ShellBuildState)).Text);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheStatusBarShowsABusyIndicatorWhileBuilding()
    {
        using var rig = SurfaceRig.Create();
        Assert.Null(rig.Find(AutomationIds.ShellBusy));

        rig.Shell.StatusBar.SetBuildState(BuildState.Building);
        rig.Settle();
        Assert.NotNull(rig.Find(AutomationIds.ShellBusy));
        Assert.Equal("Building…", Assert.IsType<TextBlock>(rig.Find(AutomationIds.ShellBusyText)).Text);

        rig.Shell.StatusBar.SetBuildState(BuildState.Idle);
        rig.Settle();
        Assert.Null(rig.Find(AutomationIds.ShellBusy));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheWindowTitleFollowsTheShell()
    {
        using var rig = SurfaceRig.Create();
        Assert.Equal("NetPrints", rig.Window.Title);
    }
}
