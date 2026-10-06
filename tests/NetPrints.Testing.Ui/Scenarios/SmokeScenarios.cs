using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.References;
using NetPrints.Testing.Ui.Screenplay;
using NetPrints.Testing.Ui.Shell;
using Xunit;

namespace NetPrints.Testing.Ui.Scenarios;

/// <summary>What a driver-specific test class provides to the shared scenarios.</summary>
/// <param name="Actor">The actor, able to <see cref="UseNetPrints"/>.</param>
/// <param name="SampleProject">A private copy of the HelloWorld sample (not opened yet).</param>
/// <param name="WorkDirectory">A private, empty folder for files the scenario creates.</param>
public sealed record SmokeContext(Actor Actor, string SampleProject, string WorkDirectory)
{
    public UseNetPrints Editor => Actor.Using<UseNetPrints>();

    public IUiDriver Driver => Editor.Driver;
}

/// <summary>
/// The smoke flows shared by every driver (headless and X11): the same Screenplay tasks and page
/// objects, one subclass per driver. Scenarios that need a capability the driver lacks are
/// skipped with <see cref="Assert.Skip(string)"/>.
/// </summary>
public abstract class SmokeScenarios
{
    protected const string MethodName = "Main";
    private const string ClassName = "Program";

    /// <summary>The Main graph of the checked-in sample: its class file and its method id.</summary>
    private static readonly DocumentId MainDocument = DocumentId.Graph("HelloWorld.Program.netpc.json", DocumentId.MethodKeyPrefix + "m000000001gs20");
    private const string DebugWriteLine = "System.Diagnostics.Debug WriteLine (value: System.Object)";

    /// <summary>A fresh editor (no project open) and a private sample copy.</summary>
    protected abstract Task<SmokeContext> StartAsync(CancellationToken cancellationToken);

    /// <summary>Records the state of every window at a step of a flow (artifact for review).</summary>
    protected abstract Task CheckpointAsync(SmokeContext context, string name, CancellationToken cancellationToken);

    protected static void Require(SmokeContext context, UiCapabilities capability)
    {
        if (!context.Driver.Capabilities.HasFlag(capability))
        {
            Assert.Skip($"The {context.Driver.Name} driver cannot do {capability}.");
        }
    }

    /// <summary>
    /// Per-step timing hook (batch D2): a no-op here so the shared scenarios cost nothing extra on
    /// the headless driver; the desktop E2E driver overrides it with a real <c>StepTimer</c>.
    /// </summary>
    protected virtual IDisposable Step(string name) => NoOpStep.Instance;

    /// <summary>Open the sample, put an If Else (condition ticked) before WriteLine, compile and run: "Hello, World!" (FR-017).</summary>
    protected async Task EditCompileAndRunAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        var actor = context.Actor;

        using (Step("open project"))
        {
            await actor.AttemptsToAsync(cancellationToken,
                OpenTheProject.At(context.SampleProject),
                OpenTheMethod.Named(MethodName));
            await CheckpointAsync(context, "01-main-graph", cancellationToken);
        }

        int nodes = await actor.AsksForAsync(TheNodeCount.OnTheCanvas(), cancellationToken);

        using (Step("edit graph"))
        {
            await actor.AttemptsToAsync(cancellationToken,
                AddANode.Named("If Else"),
                ConnectThePins.From("MethodEntryNode", "Exec", "IfElseNode", "Exec"),
                TickThePin.Of("IfElseNode", "Condition"),
                ConnectThePins.From("IfElseNode", "True", "CallMethodNode", "Exec"));
            await CheckpointAsync(context, "02-if-else-wired", cancellationToken);
        }

        Assert.Equal(nodes + 1, await actor.AsksForAsync(TheNodeCount.OnTheCanvas(), cancellationToken));

        using (Step("compile"))
        {
            await actor.AttemptsToAsync(CompileTheProject.Now(), cancellationToken);
            Assert.Equal("Build succeeded", await actor.AsksForAsync(TheBuildStatus.Now(), cancellationToken));
        }

        using (Step("run"))
        {
            await actor.AttemptsToAsync(RunTheProgram.Now(), cancellationToken);
            Assert.Contains("Hello, World!", await actor.AsksForAsync(TheProgramOutput.Containing("Hello, World!"), cancellationToken));
            await CheckpointAsync(context, "03-ran", cancellationToken);
        }
    }

    /// <summary>Create a console project through the New project dialog (PAR-02, FR-042).</summary>
    protected async Task CreateProjectAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        using (Step("create project"))
        {
            var shell = context.Editor.Shell;
            string folder = Path.Combine(context.WorkDirectory, "Created");
            string path = Path.Combine(folder, "Created.csproj");

            await shell.Menu.InvokeAsync("File", ShellCommands.NewProject, cancellationToken);
            var dialog = new NewProjectDialogPage(context.Driver);
            await dialog.WaitVisibleAsync(cancellationToken);
            await dialog.CreateAsync("Created", folder, cancellationToken);

            await shell.WaitForProjectAsync("Created", cancellationToken);
            await UiWait.UntilAsync(context.Driver, () => Task.FromResult(File.Exists(path)), "project file written", cancellationToken);
            await CheckpointAsync(context, "created-project", cancellationToken);
        }
    }

    /// <summary>Add an assembly and a source folder in the References dialog (PAR-15..18).</summary>
    protected async Task AddReferencesAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        await context.Actor.AttemptsToAsync(OpenTheProject.At(context.SampleProject), cancellationToken);
        await context.Editor.Shell.Commands.InvokeAsync(ShellCommands.References, cancellationToken);
        var references = await new ReferencesDialogPage(context.Driver).WaitShownAsync(cancellationToken);
        string sources = Directory.CreateDirectory(Path.Combine(context.WorkDirectory, "Sources")).FullName;

        using (Step("add references"))
        {
            await context.Editor.FileDialogs.OpenFileAsync("Add Assembly Reference", assemblyPath,
                () => references.AddAssemblyButton.ClickAsync(cancellationToken), cancellationToken);
            // The declared reference's Include is the assembly's simple name, no extension
            // (project-system.md §1: <Reference Include="<simple name>">).
            await references.WaitForRowAsync(Path.GetFileNameWithoutExtension(assemblyPath), cancellationToken);

            await context.Editor.FileDialogs.OpenFolderAsync("Add Source Directory", sources,
                () => references.AddSourceButton.ClickAsync(cancellationToken), cancellationToken);
            await references.WaitForRowAsync("Sources", cancellationToken);
            await CheckpointAsync(context, "references", cancellationToken);
        }

        await references.CloseAsync(cancellationToken);
    }

    /// <summary>The pointer shows the move cursor while the canvas is panned (PAR-51).</summary>
    protected async Task PanCursorAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        Require(context, UiCapabilities.RealCursor);
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named(MethodName));
        var graph = context.Editor.Shell.Graph;

        await context.Driver.MoveAsync(await graph.EmptyPointAsync(cancellationToken), cancellationToken);
        string? idle = await context.Driver.CursorNameAsync(cancellationToken);
        var at = await graph.BeginRightDragAsync(80, 40, cancellationToken);
        string? panning = await UiWait.ForAsync(context.Driver, () => context.Driver.CursorNameAsync(cancellationToken), c => c != idle,
            "the cursor to change", cancellationToken);
        await context.Driver.ReleaseAsync(at, UiButton.Right, cancellationToken);

        Assert.True(IsMoveCursor(panning), $"a move cursor while panning, not {panning} (idle: {idle})");
        await UiWait.UntilAsync(context.Driver, async () => await context.Driver.CursorNameAsync(cancellationToken) == idle, "the cursor to reset",
            cancellationToken);
    }

    /// <summary>
    /// Whether a cursor is the four-way move cursor: by name, or, for an image cursor
    /// ("image:WxH:XHOT,YHOT:SERIAL"), by its hotspot in the middle (arrows point to the hotspot
    /// at the top-left).
    /// </summary>
    public static bool IsMoveCursor(string? cursor)
    {
        if (cursor is "fleur" or "move" or "all-scroll" or "size_all")
        {
            return true;
        }

        if (cursor?.Split(':') is ["image", var size, var hot, _])
        {
            var wh = size.Split('x').Select(int.Parse).ToArray();
            var xy = hot.Split(',').Select(int.Parse).ToArray();
            return Math.Abs(xy[0] - wh[0] / 2) <= wh[0] / 8 + 1 && Math.Abs(xy[1] - wh[1] / 2) <= wh[1] / 8 + 1;
        }

        return false;
    }

    /// <summary>Real drags from the project tree's method, constructor and variable rows onto the canvas (FR-017, PAR-56, PAR-57).</summary>
    protected async Task DragFromTreeAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named(MethodName));
        var shell = context.Editor.Shell;
        var graph = shell.Graph;
        await graph.ClickEmptyAsync(cancellationToken); // ends the double click on the row, so the drag below is a first press
        int nodes = await graph.NodeCountAsync(cancellationToken);

        // Method row -> call node.
        await shell.Tree.Method(MethodName).DragToAsync(await graph.EmptyPointAsync(cancellationToken, 0, -120), cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == nodes + 1, "call node dropped", cancellationToken);

        // Variable row -> Get/Set chooser -> getter (before the constructor: its graph tab would hold a second chooser).
        await shell.Tree.SelectAsync(shell.Tree.Class(ClassName), cancellationToken);
        await shell.Menu.InvokeAsync("Edit", ShellCommands.AddVariable, cancellationToken);
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheMethod.Named(MethodName));
        await context.Driver.ClickAsync(await graph.EmptyPointAsync(cancellationToken, -200, 100), UiButton.Left, 1, cancellationToken);
        var variable = await shell.Tree.RevealAsync(shell.Tree.Variable("Variable"), ProjectTreePage.VariablesGroup, cancellationToken);
        await variable.DragToAsync(await graph.EmptyPointAsync(cancellationToken, -200, 100), cancellationToken);
        await graph.GetSet.WaitOpenAsync(cancellationToken);
        await CheckpointAsync(context, "get-set-chooser", cancellationToken);
        await graph.GetSet.GetButton.ClickAsync(cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == nodes + 2, "getter dropped", cancellationToken);

        // Constructor row -> constructor node (adding the constructor opens its graph, so reopen Main).
        await shell.Tree.SelectAsync(shell.Tree.Class(ClassName), cancellationToken);
        await shell.Menu.InvokeAsync("Edit", ShellCommands.AddConstructor, cancellationToken);
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheMethod.Named(MethodName));
        await graph.ClickEmptyAsync(cancellationToken);
        var constructor = await shell.Tree.RevealAsync(shell.Tree.Constructor(ClassName), ProjectTreePage.ConstructorsGroup, cancellationToken);
        await constructor.DragToAsync(await graph.EmptyPointAsync(cancellationToken, -150, -120), cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == nodes + 3, "constructor node dropped", cancellationToken);
        Assert.Contains("ConstructorNode", await graph.NodeNamesAsync(cancellationToken));
        await CheckpointAsync(context, "dropped-nodes", cancellationToken);
    }

    /// <summary>
    /// SC-001: open the sample, open two graphs, compile with an error, activate it from the Errors panel, fix it, run and read the
    /// output, with no editor window besides the shell.
    /// </summary>
    protected async Task ShellMainFlowAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        var actor = context.Actor;
        var shell = context.Editor.Shell;
        var graph = shell.Graph;

        using (Step("open two graphs"))
        {
            await actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named(MethodName));
            await shell.Menu.InvokeAsync("Edit", ShellCommands.AddConstructor, cancellationToken);
            await shell.OpenMethodAsync(MethodName, cancellationToken);
            await CheckpointAsync(context, "01-two-graphs", cancellationToken);
        }

        using (Step("break and compile"))
        {
            await shell.Tree.SelectAsync(shell.Tree.Class(ClassName), cancellationToken);
            await shell.Menu.InvokeAsync("Edit", ShellCommands.AddVariable, cancellationToken);
            await shell.OpenMethodAsync(MethodName, cancellationToken);
            await graph.ClickEmptyAsync(cancellationToken);
            var variable = await shell.Tree.RevealAsync(shell.Tree.Variable("Variable"), ProjectTreePage.VariablesGroup, cancellationToken);
            await variable.DragToAsync(await graph.EmptyPointAsync(cancellationToken, -545, -346), cancellationToken);
            await graph.GetSet.WaitOpenAsync(cancellationToken);
            await graph.GetSet.GetButton.ClickAsync(cancellationToken);
            await graph.Node("VariableGetterNode").GetAsync(cancellationToken);
            await actor.AttemptsToAsync(cancellationToken, new AddANode("debug write line", DebugWriteLine, 280, 270),
                ConnectThePins.From("MethodEntryNode", "Exec", "CallMethodNode2", "Exec"),
                ConnectThePins.From("VariableGetterNode", "Object", "CallMethodNode2", "value"));
            await actor.AttemptsToAsync(CompileTheProject.Now(), cancellationToken);
            Assert.StartsWith("Build failed", await actor.AsksForAsync(TheBuildStatus.Now(), cancellationToken), StringComparison.Ordinal);
            await shell.Bottom.ShowAsync(PanelContributions.ErrorsId, cancellationToken);
            await shell.Bottom.ErrorRow().WaitVisibleAsync(cancellationToken);
            await CheckpointAsync(context, "02-failed", cancellationToken);
        }

        using (Step("activate the error"))
        {
            var constructor = await shell.Tree.RevealAsync(shell.Tree.Constructor(ClassName), ProjectTreePage.ConstructorsGroup, cancellationToken);
            await shell.Tree.OpenAsync(constructor, async () => (await graph.Watermark.TryGetAsync(cancellationToken))?.Text is { } shown && shown != MethodName,
                "the constructor graph shown", cancellationToken);

            await shell.Bottom.ShowAsync(PanelContributions.ErrorsId, cancellationToken);
            await shell.Bottom.ErrorRow().DoubleClickAsync(cancellationToken);
            await graph.WaitForGraphAsync(MethodName, cancellationToken);
            await UiWait.UntilAsync(context.Driver, () => graph.Node("CallMethodNode2").IsSelectedAsync(cancellationToken), "the failing node selected", cancellationToken);
            await CheckpointAsync(context, "03-error-activated", cancellationToken);
            Assert.Equal(["ShellWindow"], await shell.WindowTypesAsync(cancellationToken));
        }

        using (Step("fix, compile and run"))
        {
            foreach (string node in new[] { "CallMethodNode2", "VariableGetterNode" })
            {
                await graph.Node(node).SelectAsync(cancellationToken);
                await context.Driver.PressAsync("Delete", cancellationToken);
                await UiWait.UntilAsync(context.Driver, async () => !(await graph.NodeNamesAsync(cancellationToken)).Contains(node), $"{node} deleted", cancellationToken);
            }

            await actor.AttemptsToAsync(cancellationToken, ConnectThePins.From("MethodEntryNode", "Exec", "CallMethodNode", "Exec"));
            await actor.AttemptsToAsync(CompileTheProject.Now(), cancellationToken);
            Assert.Equal("Build succeeded", await actor.AsksForAsync(TheBuildStatus.Now(), cancellationToken));
            await actor.AttemptsToAsync(RunTheProgram.Now(), cancellationToken);
            Assert.Contains("Hello, World!", await actor.AsksForAsync(TheProgramOutput.Containing("Hello, World!"), cancellationToken));
            await CheckpointAsync(context, "04-ran", cancellationToken);
        }

        Assert.Equal(["ShellWindow"], await shell.WindowTypesAsync(cancellationToken));
    }

    /// <summary>Float a graph tab, edit, undo, redo and save in its window, dock it back, then float it again and close its window (FR-016, US2 scenario 7).</summary>
    protected async Task FloatAndRedockGraphAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        var actor = context.Actor;
        var shell = context.Editor.Shell;
        await actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named(MethodName));
        int nodes = await shell.Graph.NodeCountAsync(cancellationToken);

        using (Step("float"))
        {
            await FloatMainAsync(context, cancellationToken);
            await CheckpointAsync(context, "floated", cancellationToken);
        }

        using (Step("edit, undo, save in the floated window"))
        {
            var floated = shell.GraphOf(MainDocument);
            await actor.AttemptsToAsync(cancellationToken, AddANode.Named("If Else").In(MainDocument));
            Assert.Equal(nodes + 1, await floated.NodeCountAsync(cancellationToken));
            string classFile = Path.Combine(Path.GetDirectoryName(context.SampleProject) ?? "", "HelloWorld.Program.netpc.json");
            Assert.DoesNotContain("\"Variable\"", await File.ReadAllTextAsync(classFile, cancellationToken), StringComparison.Ordinal);


            // Node edits record no undo step; the class variable does, and it belongs to the floated graph's class.
            var variable = shell.Tree.Variable("Variable");
            await shell.Menu.InvokeAsync("Edit", ShellCommands.AddVariable, cancellationToken);
            await shell.Tree.RevealAsync(variable, ProjectTreePage.VariablesGroup, cancellationToken);
            await floated.ClickEmptyAsync(cancellationToken);
            await context.Driver.PressAsync("Ctrl+Z", cancellationToken);
            await UiWait.UntilAsync(context.Driver, async () => !await variable.ExistsAsync(cancellationToken), "the variable removed by undo", cancellationToken);
            await floated.ClickEmptyAsync(cancellationToken);
            await context.Driver.PressAsync("Ctrl+Y", cancellationToken);
            await UiWait.UntilAsync(context.Driver, async () => await variable.ExistsAsync(cancellationToken), "the variable restored by redo", cancellationToken);
            Assert.Equal(nodes + 1, await floated.NodeCountAsync(cancellationToken));

            await context.Driver.PressAsync("Ctrl+S", cancellationToken);
            await UiWait.UntilAsync(context.Driver, async () => (await File.ReadAllTextAsync(classFile, cancellationToken)).Contains("\"Variable\"", StringComparison.Ordinal),
                "the class saved with the variable", cancellationToken);
            await CheckpointAsync(context, "floated-saved", cancellationToken);
        }

        using (Step("dock back"))
        {
            await shell.Menu.InvokeAsync("View", ShellCommands.DockDocument, cancellationToken);
            await WaitDockedAsync(context, nodes + 1, cancellationToken);
        }

        using (Step("close a floated graph window"))
        {
            await FloatMainAsync(context, cancellationToken);
            var window = (await shell.GraphOf(MainDocument).GetAsync(cancellationToken)).Window;
            await context.Driver.CloseWindowAsync(window, cancellationToken);
            await UiWait.UntilAsync(context.Driver, async () => (await shell.WindowTypesAsync(cancellationToken)).Count == 1, "the floated window gone", cancellationToken);
            Assert.False(await shell.Tabs.Tab(MainDocument).ExistsAsync(cancellationToken), "a floated graph closes like a tab");
        }

        if (context.Driver.Capabilities.HasFlag(UiCapabilities.WindowManager))
        {
            using (Step("float a pane and close its window"))
            {
                var tab = shell.PanelTab(PanelContributions.OutputId);
                var outside = (await tab.CenterAsync(cancellationToken)).Offset(300, 100);
                await tab.DragToAsync(outside, cancellationToken);
                await UiWait.UntilAsync(context.Driver, async () => (await shell.WindowTypesAsync(cancellationToken)).Count == 2, "the pane floated", cancellationToken);
                var pane = shell.PanelContent(PanelContributions.OutputId);
                await context.Driver.CloseWindowAsync((await pane.GetAsync(cancellationToken)).Window, cancellationToken);
                await UiWait.UntilAsync(context.Driver, async () => (await shell.WindowTypesAsync(cancellationToken)).Count == 1, "the pane's window gone", cancellationToken);
                await shell.PanelTab(PanelContributions.OutputId).WaitVisibleAsync(cancellationToken);
                await CheckpointAsync(context, "pane-docked-back", cancellationToken);
            }
        }
    }

    /// <summary>View > Reset layout takes a floated graph back into the default layout (FR-015).</summary>
    protected async Task ResetLayoutAsync(CancellationToken cancellationToken)
    {
        SmokeContext context;
        using (Step("start"))
        {
            context = await StartAsync(cancellationToken);
        }

        var shell = context.Editor.Shell;
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named(MethodName));
        int nodes = await shell.Graph.NodeCountAsync(cancellationToken);

        using (Step("float and reset"))
        {
            await FloatMainAsync(context, cancellationToken);
            await shell.Menu.InvokeAsync("View", ShellCommands.ResetLayout, cancellationToken);
            await WaitDockedAsync(context, nodes, cancellationToken);
            await shell.Tree.Project("HelloWorld").WaitVisibleAsync(cancellationToken);
            await shell.Inspector.WaitVisibleAsync(cancellationToken);
            await shell.Bottom.ShowAsync(PanelContributions.ErrorsId, cancellationToken);
            await CheckpointAsync(context, "reset", cancellationToken);
        }
    }

    private async Task FloatMainAsync(SmokeContext context, CancellationToken cancellationToken)
    {
        var shell = context.Editor.Shell;
        await shell.Menu.InvokeAsync("View", ShellCommands.FloatDocument, cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => (await shell.WindowTypesAsync(cancellationToken)).Count == 2, "the floated window", cancellationToken);
        var floated = shell.GraphOf(MainDocument);
        await floated.WaitForGraphAsync(MethodName, cancellationToken);
        Assert.False(await shell.Tabs.Tab(MainDocument).ExistsAsync(cancellationToken), "the tab left the shell window");

        // The window manager may place the new window over the menu bar; park it over the empty document area.
        var origin = (await shell.GetAsync(cancellationToken)).ScreenBounds;
        await context.Driver.MoveWindowAsync((await floated.GetAsync(cancellationToken)).Window, origin.X + 270, origin.Y + 120, cancellationToken);
    }

    private static async Task WaitDockedAsync(SmokeContext context, int nodes, CancellationToken cancellationToken)
    {
        var shell = context.Editor.Shell;
        await UiWait.UntilAsync(context.Driver, async () => (await shell.WindowTypesAsync(cancellationToken)).Count == 1, "the floated window gone", cancellationToken);
        await shell.Tabs.WaitOpenAsync(MainDocument, cancellationToken);
        await shell.Tabs.SelectAsync(MainDocument, cancellationToken);
        await shell.Graph.WaitForGraphAsync(MethodName, cancellationToken);
        Assert.Equal(nodes, await shell.Graph.NodeCountAsync(cancellationToken));
    }

    private sealed class NoOpStep : IDisposable
    {
        public static readonly NoOpStep Instance = new();

        public void Dispose()
        {
        }
    }
}
