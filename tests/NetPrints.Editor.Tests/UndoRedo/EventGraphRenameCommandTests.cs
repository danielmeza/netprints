using NetPrints.Core;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>US8: <see cref="EditorCommands.RenameEventGraph"/> renames an event graph as one undo step.</summary>
public sealed class EventGraphRenameCommandTests
{
    [Fact]
    public void RenamingIsOneUndoStepAndRedoes()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var graph = new EventGraph("First") { Class = cls };
        cls.EventGraphs.Add(graph);
        var stack = new UndoRedoStack();

        stack.Do(EditorCommands.RenameEventGraph(graph, "Gameplay"));

        Assert.Equal("Gameplay", graph.Name);

        stack.Undo();

        Assert.Equal("First", graph.Name);

        stack.Redo();

        Assert.Equal("Gameplay", graph.Name);
    }

    [Fact]
    public void ADuplicateNameIsRefusedAndLeavesNothingToUndo()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        var graph = new EventGraph("First") { Class = cls };
        var other = new EventGraph("Second") { Class = cls };
        cls.EventGraphs.Add(graph);
        cls.EventGraphs.Add(other);
        var stack = new UndoRedoStack();

        ArgumentException refused = Assert.Throws<ArgumentException>(() => stack.Do(EditorCommands.RenameEventGraph(graph, "Second")));

        Assert.Contains("An event graph named 'Second' already exists", refused.Message, StringComparison.Ordinal);
        Assert.Equal("First", graph.Name);
        Assert.False(stack.CanUndo);
    }
}
