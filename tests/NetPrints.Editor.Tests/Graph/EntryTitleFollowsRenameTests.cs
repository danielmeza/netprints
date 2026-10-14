using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>The title of an entry node follows a rename of its event or method, undo and redo included.</summary>
public sealed class EntryTitleFollowsRenameTests : IDisposable
{
    private readonly ClassContext context;

    public EntryTitleFollowsRenameTests(TestEditor editor)
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
    public void TheTitleOfACustomEventEntryFollowsTheEventName()
    {
        EventGraph events = context.CreateEventGraph();
        var entry = new EventEntryNode(events, "CustomEvent");
        using var graph = new NodeGraphViewModel(events, context.Services);
        NodeViewModel entryViewModel = graph.Nodes.Single(n => n.Node == entry);
        List<string?> raised = Watch(entryViewModel);

        context.RenameEvent(entry, "OnHit");

        Assert.Equal("OnHit Entry", entryViewModel.Label);
        Assert.Contains(nameof(NodeViewModel.Label), raised);

        raised.Clear();
        context.UndoRedo.Undo();

        Assert.Equal("CustomEvent Entry", entryViewModel.Label);
        Assert.Contains(nameof(NodeViewModel.Label), raised);

        raised.Clear();
        context.UndoRedo.Redo();

        Assert.Equal("OnHit Entry", entryViewModel.Label);
        Assert.Contains(nameof(NodeViewModel.Label), raised);
    }

    [Fact]
    public void TheTitleOfAMethodEntryFollowsTheMethodName()
    {
        MethodGraph method = context.CreateMethod();
        using var graph = new NodeGraphViewModel(method, context.Services);
        NodeViewModel entryViewModel = graph.Nodes.Single(n => n.Node == method.EntryNode);
        string oldTitle = entryViewModel.Label;
        List<string?> raised = Watch(entryViewModel);

        context.RenameMethod(method, "Fire");

        Assert.Equal("Fire Entry", entryViewModel.Label);
        Assert.Contains(nameof(NodeViewModel.Label), raised);

        raised.Clear();
        context.UndoRedo.Undo();

        Assert.Equal(oldTitle, entryViewModel.Label);
        Assert.Contains(nameof(NodeViewModel.Label), raised);
    }
}
