using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Screenplay;
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

/// <summary>Smoke flows shared by every driver; one subclass per driver.</summary>
public abstract class SmokeScenarios
{
    protected const string ClassName = "HelloWorld.Program";

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

    /// <summary>Open the sample, put an If Else (condition ticked) before WriteLine, compile and run: "Hello, World!" (FR-017).</summary>
    protected async Task EditCompileAndRunAsync(CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        var actor = context.Actor;

        await actor.AttemptsToAsync(cancellationToken,
            OpenTheProject.At(context.SampleProject),
            OpenTheMethod.Named("Main").Of(ClassName));
        await CheckpointAsync(context, "01-main-graph", cancellationToken);
        int nodes = await actor.AsksForAsync(TheNodeCount.In(ClassName), cancellationToken);

        await actor.AttemptsToAsync(cancellationToken,
            AddANode.Named("If Else", ClassName),
            ConnectThePins.From(ClassName, "MethodEntryNode", "Exec", "IfElseNode", "Exec"),
            TickThePin.Of(ClassName, "IfElseNode", "Condition"),
            ConnectThePins.From(ClassName, "IfElseNode", "True", "CallMethodNode", "Exec"));
        await CheckpointAsync(context, "02-if-else-wired", cancellationToken);
        Assert.Equal(nodes + 1, await actor.AsksForAsync(TheNodeCount.In(ClassName), cancellationToken));

        await actor.AttemptsToAsync(CompileTheProject.From(ClassName), cancellationToken);
        Assert.Equal("Build succeeded", await actor.AsksForAsync(TheBuildStatus.In(ClassName), cancellationToken));

        Require(context, UiCapabilities.ProcessOutput);
        await actor.AttemptsToAsync(RunTheProgram.From(ClassName), cancellationToken);
        Assert.Contains("Hello, World!", await actor.AsksForAsync(TheProgramOutput.Containing("Hello, World!"), cancellationToken));
        await CheckpointAsync(context, "03-ran", cancellationToken);
    }

    /// <summary>Create a project through the save picker (PAR-02).</summary>
    protected async Task CreateProjectAsync(CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        var main = await context.Editor.MainWindow.ShowProjectPaneAsync(cancellationToken);
        string path = Path.Combine(context.WorkDirectory, "Created.netpp");

        await context.Editor.FileDialogs.SaveFileAsync("Create Project", path, () => main.CreateProjectButton.ClickAsync(cancellationToken), cancellationToken);

        await main.WaitForProjectAsync("Created", cancellationToken);
        await UiWait.UntilAsync(context.Driver, () => Task.FromResult(File.Exists(path)), "project file written", cancellationToken);
        await CheckpointAsync(context, "created-project", cancellationToken);
    }

    /// <summary>Add an assembly and a source folder in the References dialog (PAR-15..18).</summary>
    protected async Task AddReferencesAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        await context.Actor.AttemptsToAsync(OpenTheProject.At(context.SampleProject), cancellationToken);
        var references = await context.Editor.MainWindow.OpenReferencesAsync(cancellationToken);
        string sources = Directory.CreateDirectory(Path.Combine(context.WorkDirectory, "Sources")).FullName;

        await context.Editor.FileDialogs.OpenFileAsync("Add Assembly Reference", assemblyPath,
            () => references.AddAssemblyButton.ClickAsync(cancellationToken), cancellationToken);
        await references.WaitForRowAsync(Path.GetFileName(assemblyPath), cancellationToken);

        await context.Editor.FileDialogs.OpenFolderAsync("Add Source Directory", sources,
            () => references.AddSourceButton.ClickAsync(cancellationToken), cancellationToken);
        await references.WaitForRowAsync("Sources", cancellationToken);
        await CheckpointAsync(context, "references", cancellationToken);

        await references.CloseAsync(cancellationToken);
    }

    /// <summary>A minimized class window is restored, not duplicated, from the class list (PAR-14).</summary>
    protected async Task MinimizeAndRestoreClassWindowAsync(CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        Require(context, UiCapabilities.WindowManager);
        await context.Actor.AttemptsToAsync(OpenTheProject.At(context.SampleProject), cancellationToken);
        var main = context.Editor.MainWindow;
        var page = await main.OpenClassAsync(ClassName, cancellationToken);
        string window = (await page.GetAsync(cancellationToken)).Window;

        await context.Driver.MinimizeAsync(window, cancellationToken);
        await UiWait.UntilAsync(context.Driver, () => context.Driver.IsMinimizedAsync(window, cancellationToken), "class window minimized", cancellationToken);

        await main.ClassButton(ClassName).ClickAsync(cancellationToken);

        await UiWait.UntilAsync(context.Driver, async () => !await context.Driver.IsMinimizedAsync(window, cancellationToken), "class window restored",
            cancellationToken);
        var windows = await context.Driver.FindAllAsync(page.Query, cancellationToken);
        Assert.Single(windows);
        Assert.Equal(window, windows[0].Window);
        await CheckpointAsync(context, "restored", cancellationToken);
    }

    /// <summary>The pointer shows the move cursor while the canvas is panned (PAR-51).</summary>
    protected async Task PanCursorAsync(CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        Require(context, UiCapabilities.RealCursor);
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named("Main").Of(ClassName));
        var graph = context.Editor.ClassEditor(ClassName).Graph;

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

    /// <summary>Whether a cursor is the move cursor: by name, or by a centered hotspot for an image cursor.</summary>
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

    /// <summary>Real drags from the method, constructor and variable lists onto the canvas (PAR-56, 57).</summary>
    protected async Task DragFromListsAsync(CancellationToken cancellationToken)
    {
        var context = await StartAsync(cancellationToken);
        Require(context, UiCapabilities.OsDragDrop);
        await context.Actor.AttemptsToAsync(cancellationToken, OpenTheProject.At(context.SampleProject), OpenTheMethod.Named("Main").Of(ClassName));
        var page = context.Editor.ClassEditor(ClassName);
        var graph = page.Graph;
        int nodes = await graph.NodeCountAsync(cancellationToken);

        // Method list -> call node.
        await page.Method("Main").DragToAsync(await graph.EmptyPointAsync(cancellationToken, -400, -350), cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == nodes + 1, "call node dropped", cancellationToken);

        // Constructor list -> constructor call node.
        await page.CreateConstructorButton.ClickAsync(cancellationToken); // opens the new constructor's graph
        await page.OpenMethodAsync("Main", cancellationToken);
        await page.Constructor(0).DragToAsync(await graph.EmptyPointAsync(cancellationToken, -400, -150), cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == nodes + 2, "constructor node dropped",
            cancellationToken);
        Assert.Contains("ConstructorNode", await graph.NodeNamesAsync(cancellationToken));
        int afterConstructor = nodes + 2;

        // Variable list -> Get/Set chooser -> getter.
        await page.CreateVariableButton.ClickAsync(cancellationToken);
        await page.VariableNameText("Variable").DragToAsync(await graph.EmptyPointAsync(cancellationToken, -150, -350), cancellationToken);
        await graph.GetSet.WaitOpenAsync(cancellationToken);
        await CheckpointAsync(context, "get-set-chooser", cancellationToken);
        await graph.GetSet.GetButton.ClickAsync(cancellationToken);
        await UiWait.UntilAsync(context.Driver, async () => await graph.NodeCountAsync(cancellationToken) == afterConstructor + 1, "getter dropped",
            cancellationToken);
        await CheckpointAsync(context, "dropped-nodes", cancellationToken);
    }
}
