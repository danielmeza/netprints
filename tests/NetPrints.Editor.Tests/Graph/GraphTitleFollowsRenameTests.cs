using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>The label in the canvas corner follows a rename of its graph, undo and redo included.</summary>
public sealed class GraphTitleFollowsRenameTests : IDisposable
{
    private readonly ClassContext context;

    public GraphTitleFollowsRenameTests(TestEditor editor)
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        context = new ClassContext(cls, editor.Context, new UndoRedoStack());
    }

    public void Dispose() => context.Dispose();

    private static List<string?> Watch(System.ComponentModel.INotifyPropertyChanged source)
    {
        List<string?> raised = [];
        source.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    [Fact]
    public void TheLabelOfAnEventGraphFollowsItsRename()
    {
        EventGraph events = context.CreateEventGraph();
        using var graph = new NodeGraphViewModel(events, context.Services);
        List<string?> raised = Watch(graph);

        context.RenameEventGraph(events, "Gameplay");

        Assert.Equal("Gameplay", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);

        raised.Clear();
        context.UndoRedo.Undo();

        Assert.Equal("EventGraph", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);

        raised.Clear();
        context.UndoRedo.Redo();

        Assert.Equal("Gameplay", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);
    }

    [Fact]
    public void TheLabelOfAMethodFollowsItsRename()
    {
        MethodGraph method = context.CreateMethod();
        using var graph = new NodeGraphViewModel(method, context.Services);
        string oldName = method.Name;
        List<string?> raised = Watch(graph);

        context.RenameMethod(method, "Fire");

        Assert.Equal("Fire", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);

        raised.Clear();
        context.UndoRedo.Undo();

        Assert.Equal(oldName, graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);
    }

    [Fact]
    public void TheLabelOfAConstructorFollowsTheClassRename()
    {
        ConstructorGraph constructor = context.CreateConstructor();
        using var graph = new NodeGraphViewModel(constructor, context.Services);
        List<string?> raised = Watch(graph);

        context.ClassInspector.Name = "Renamed";

        Assert.Equal("Renamed", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);

        raised.Clear();
        context.UndoRedo.Undo();

        Assert.Equal("C", graph.Name);
        Assert.Contains(nameof(NodeGraphViewModel.Name), raised);
    }
}
