using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The HelloWorld sample open in the shell with <c>Program.Main</c> on the canvas.</summary>
internal sealed class EditorSession : IAsyncDisposable
{
    public const string ClassName = "HelloWorld.Program";

    private EditorSession(ShellApp app, ShellPage page)
    {
        App = app;
        Page = page;
        Class = app.Session.Project.Classes.Single(c => c.FullName == ClassName);
    }

    public ShellApp App { get; }

    public SampleCopy Sample => App.Sample;

    public HeadlessDriver Driver => App.Driver;

    /// <summary>Page objects (act).</summary>
    public ShellPage Page { get; }

    public GraphCanvas Graph => Page.Graph;

    /// <summary>View models and window (arrange and assert through the API).</summary>
    public ShellWindow Window => App.Window;

    public ClassGraph Class { get; }

    public ClassContext ClassContext => App.Session.ContextFor(Class);

    public NodeGraphViewModel GraphViewModel => Assert.IsType<GraphDocumentViewModel>(App.Shell.ActiveDocument).Graph;

    public static async Task<EditorSession> OpenSampleMainAsync(CancellationToken cancellationToken, Action? appStarted = null)
    {
        var app = ShellApp.Start();
        appStarted?.Invoke();
        UseFixedSize(app.Window);
        await app.OpenSampleAsync(cancellationToken);
        var page = await new ShellPage(app.Driver).WaitShownAsync(cancellationToken);
        await page.OpenMethodAsync("Main", cancellationToken);
        var session = new EditorSession(app, page);
        await session.WaitForRenderedAsync(cancellationToken);
        return session;
    }

    /// <summary>
    /// Opens another graph, then opens <c>Main</c> by double-clicking its row in the Project tree, so focus starts in the tree
    /// and only the graph opening can move it to the canvas.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when <c>Main</c> is on the canvas.</returns>
    public async Task PickMainFromTheTreeAsync(CancellationToken cancellationToken)
    {
        MethodGraph other = ClassContext.CreateMethod();
        App.Api.OpenDocument(CommandTargets.GraphDocumentOf(App.Session, other) ?? throw new InvalidOperationException("The new method has no document."));
        await Graph.WaitForGraphAsync(other.Name, cancellationToken);
        await Page.OpenMethodAsync("Main", cancellationToken);
        await WaitForRenderedAsync(cancellationToken);
    }

    /// <summary>Opens the class graph in its own document and waits for its canvas.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the class graph is on the canvas.</returns>
    public async Task OpenClassGraphAsync(CancellationToken cancellationToken)
    {
        App.Api.OpenDocument(CommandTargets.GraphDocumentOf(App.Session, Class) ?? throw new InvalidOperationException("The class has no document."));
        await Graph.Watermark.WaitUntilAsync(e => e.Text == Class.Name, "the class graph shown", cancellationToken);
    }

    /// <summary>Runs the Add variable command on the active class.</summary>
    /// <param name="cancellationToken">Cancels the command.</param>
    public Task AddVariableAsync(CancellationToken cancellationToken) => RunAsync("addVariable", cancellationToken);

    /// <summary>Runs a registered command through the invoker, as a menu or key does, so it must be enabled in the current context.</summary>
    /// <param name="name">The command name after the contribution prefix.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="scope">The scope the invocation comes from.</param>
    /// <returns>A task that completes when the command has run.</returns>
    public Task RunAsync(string name, CancellationToken cancellationToken, CommandScope scope = CommandScope.Global)
    {
        Assert.True(App.Commands.TryRun(App.Command(name), scope), $"{name} is enabled");
        HeadlessDriver.Pump();
        return Task.CompletedTask;
    }

    public Task PressUndoAsync(CancellationToken cancellationToken) => Driver.PressAsync("Ctrl+Z", cancellationToken);

    public Task PressRedoAsync(CancellationToken cancellationToken) => Driver.PressAsync("Ctrl+Y", cancellationToken);

    public Task PressDeleteAsync(CancellationToken cancellationToken) => Driver.PressAsync("Delete", cancellationToken);

    /// <summary>A fixed window size, so pointer coordinates and snapshots are predictable.</summary>
    private static void UseFixedSize(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.Width = HeadlessApp.ScreenWidth;
        window.Height = HeadlessApp.ScreenHeight;
        HeadlessDriver.Pump();
    }

    /// <summary>Waits until every node and cable of the view model is on the canvas.</summary>
    public Task WaitForRenderedAsync(CancellationToken cancellationToken) =>
        UiWait.UntilAsync(Driver, async () =>
            await Graph.NodeCountAsync(cancellationToken) == GraphViewModel.Nodes.Count
            && (await Graph.ConnectionNamesAsync(cancellationToken)).Count == GraphViewModel.Connections.Count,
            "nodes and cables realized", cancellationToken);

    public ValueTask DisposeAsync() => App.DisposeAsync();
}
