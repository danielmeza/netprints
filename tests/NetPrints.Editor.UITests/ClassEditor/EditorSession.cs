using Avalonia.Controls;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.ClassEditor;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;

namespace NetPrints.Editor.UITests.ClassEditor;

/// <summary>The HelloWorld sample open in the editor with <c>Program.Main</c> on the canvas.</summary>
public sealed class EditorSession : IAsyncDisposable
{
    public const string ClassName = "HelloWorld.Program";

    private EditorSession(SampleCopy sample, HeadlessApp app, ClassEditorPage classEditor, GraphCanvas graph)
    {
        Sample = sample;
        App = app;
        ClassEditor = classEditor;
        Graph = graph;
    }

    public SampleCopy Sample { get; }
    public HeadlessApp App { get; }
    public IUiDriver Driver => App.Driver;

    /// <summary>Page objects (act).</summary>
    public ClassEditorPage ClassEditor { get; }
    public GraphCanvas Graph { get; }

    /// <summary>View models and window (arrange and assert through the API).</summary>
    public ClassEditorWindow ClassWindow => App.ClassWindow(ClassName);
    public ClassEditorViewModel ClassViewModel => (ClassEditorViewModel)ClassWindow.DataContext!;
    public NodeGraphViewModel GraphViewModel => ClassViewModel.OpenedGraph!;

    public static async Task<EditorSession> OpenSampleMainAsync(CancellationToken cancellationToken)
    {
        var sample = new SampleCopy();
        var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, cancellationToken);
        var classEditor = await app.Main.OpenClassAsync(ClassName, cancellationToken);
        UseFixedSize(app.ClassWindow(ClassName));
        var graph = await classEditor.OpenMethodAsync("Main", cancellationToken);
        var session = new EditorSession(sample, app, classEditor, graph);
        await session.WaitForRenderedAsync(cancellationToken);
        return session;
    }

    /// <summary>
    /// Opens another graph, then picks <c>Main</c> in the method list with a single click, so focus starts in the list
    /// and only the graph opening can move it to the canvas.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when <c>Main</c> is on the canvas.</returns>
    public async Task PickMainFromTheMethodListAsync(CancellationToken cancellationToken)
    {
        ClassViewModel.CreateMethodCommand.Execute(null);
        await ClassEditor.Method("Main").ClickAsync(cancellationToken);
        await ClassEditor.Graph.WaitForGraphAsync("Main", cancellationToken);
        await WaitForRenderedAsync(cancellationToken);
    }

    /// <summary>A fixed window size, so pointer coordinates and snapshots are predictable.</summary>
    private static void UseFixedSize(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.Width = 1600;
        window.Height = 1000;
        HeadlessDriver.Pump();
    }

    /// <summary>Waits until every node and cable of the view model is on the canvas.</summary>
    public Task WaitForRenderedAsync(CancellationToken cancellationToken) =>
        UiWait.UntilAsync(Driver, async () =>
            await Graph.NodeCountAsync(cancellationToken) == GraphViewModel.Nodes.Count
            && (await Graph.ConnectionNamesAsync(cancellationToken)).Count == GraphViewModel.Connections.Count,
            "nodes and cables realized", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        Sample.Dispose();
    }
}
