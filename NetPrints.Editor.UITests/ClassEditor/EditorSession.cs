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
public sealed class EditorSession : IDisposable
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
    public ClassEditorVM ClassVM => (ClassEditorVM)ClassWindow.DataContext!;
    public NodeGraphVM GraphVM => ClassVM.OpenedGraph!;

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
            await Graph.NodeCountAsync(cancellationToken) == GraphVM.Nodes.Count
            && (await Graph.ConnectionNamesAsync(cancellationToken)).Count == GraphVM.Connections.Count,
            "nodes and cables realized", cancellationToken);

    public void Dispose()
    {
        App.Dispose();
        Sample.Dispose();
    }
}
