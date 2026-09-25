using Avalonia;
using Avalonia.Controls;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Tests.Ui;

/// <summary>The sample project open in a class window with Main in the canvas.</summary>
public sealed class GraphTestContext : IDisposable
{
    private GraphTestContext() { }

    public string ProjectPath { get; private init; } = "";
    public EditorComposition Composition { get; private init; } = null!;
    public MainWindow MainWindow { get; private init; } = null!;
    public ClassEditorWindow Window { get; private init; } = null!;
    public ClassEditorVM Editor { get; private init; } = null!;
    public FakeDialogs Dialogs { get; private init; } = null!;
    public GraphEditorView View { get; private init; } = null!;
    public NodeGraphVM Graph => Editor.OpenedGraph!;

    public static async Task<GraphTestContext> OpenSampleMainAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        var (composition, mainWindow, dialogs, _) = UiHelpers.StartEditor();
        var main = composition.MainEditor!;
        await main.LoadProjectAsync(path);
        await UiTest.WaitUntilAsync(() => composition.Context.Reflection.NonStaticTypes.Count > 0, 60000, "reflection loaded");

        var (window, editor) = await UiHelpers.OpenClassAsync(composition, main);
        editor.OpenMethodCommand.Execute(editor.Methods.Single());

        // A fixed size makes pointer coordinates predictable.
        window.WindowState = WindowState.Normal;
        window.Width = 1600;
        window.Height = 1000;

        var context = new GraphTestContext
        {
            ProjectPath = path,
            Composition = composition,
            MainWindow = mainWindow,
            Window = window,
            Editor = editor,
            Dialogs = dialogs,
            View = window.Descendants<GraphEditorView>().Single(),
        };

        await context.WaitForGraphAsync();
        return context;
    }

    public async Task WaitForGraphAsync()
    {
        await UiTest.WaitUntilAsync(() => Window.Descendants<ItemContainer>().Count() == Graph.Nodes.Count
            && Window.Descendants<Connection>().Count() == Graph.Connections.Count);
    }

    public ItemContainer ContainerOf(NodeVM node) => Window.Descendants<ItemContainer>().Single(c => c.DataContext == node);

    public Connector ConnectorOf(NodePinVM pin) => Window.Descendants<Connector>().Single(c => c.DataContext == pin);

    /// <summary>Center of a pin's connector shape in window coordinates.</summary>
    public Point PinPoint(NodePinVM pin)
    {
        var connector = ConnectorOf(pin);
        var shape = connector.Descendants<Avalonia.Controls.Shapes.Shape>().First(s => s.IsVisible);
        return shape.CenterIn(Window);
    }

    /// <summary>A point on the canvas (window coordinates) away from nodes.</summary>
    public Point EmptyCanvasPoint(double dx = 0, double dy = 0)
    {
        var editor = View.Find<NodifyEditor>("Editor");
        var origin = editor.TranslatePoint(new Point(0, 0), Window)!.Value;
        return new Point(origin.X + editor.Bounds.Width - 150 + dx, origin.Y + editor.Bounds.Height - 150 + dy);
    }

    public void Dispose()
    {
        MainWindow.Close();
        TestPaths.TryDelete(ProjectPath);
    }
}
