using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.ClassEditor;

/// <summary>Editor dirty tracking (editor-services.md §3, ED-T15).</summary>
public class DirtyTrackingTests(TestEditor editor) : IAsyncLifetime
{
    private Project? projectField;
    private ClassGraph? clsAField;
    private ClassGraph? clsBField;
    private ClassEditorViewModel? vmAField;
    private ClassEditorViewModel? vmBField;

    private Project project => projectField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassGraph clsA => clsAField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassGraph clsB => clsBField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassEditorViewModel vmA => vmAField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");
    private ClassEditorViewModel vmB => vmBField ?? throw new InvalidOperationException($"{nameof(InitializeAsync)} has not run yet.");

    /// <summary>Loads a project with two classes, saves it once so both start clean, then opens both editors.</summary>
    public async ValueTask InitializeAsync()
    {
        projectField = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        clsAField = project.Classes.Single();
        clsBField = project.CreateNewClass(DefaultProjectProfile.Instance);

        await editor.Context.Persistence.SaveAsync(project, _ => "// generated\n", TestContext.Current.CancellationToken);
        Assert.False(clsA.IsDirty);
        Assert.False(clsB.IsDirty);

        vmAField = new ClassEditorViewModel(clsA, editor.Context);
        vmBField = new ClassEditorViewModel(clsB, editor.Context);
    }

    public ValueTask DisposeAsync()
    {
        vmAField?.Dispose();
        vmBField?.Dispose();
        TestPaths.TryDelete(projectField?.Path);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PanZoomAndSelectionDoNotMarkDirty()
    {
        // Pan/zoom live entirely in the Avalonia view (GraphEditorView/Nodify) and never touch the
        // model; selection is a plain property with no MarkDirty call attached.
        vmA.SelectedMethod = vmA.Methods.SingleOrDefault();
        vmA.SelectedBottomTab = 1;

        Assert.False(clsA.IsDirty);
        Assert.False(clsB.IsDirty);

        var result = await editor.Context.Persistence.SaveAsync(project, _ => "// generated\n", TestContext.Current.CancellationToken);
        Assert.Empty(result.WrittenFiles);
    }

    [Fact]
    public async Task MovingANodeMarksOnlyThatClassDirtyAndSavesOnlyItsFiles()
    {
        Node node = vmA.Methods.Single().Graph.Nodes.First();
        node.PositionX += 10;

        Assert.True(clsA.IsDirty);
        Assert.False(clsB.IsDirty);

        var result = await editor.Context.Persistence.SaveAsync(project, _ => "// generated\n", TestContext.Current.CancellationToken);

        string graphPath = project.GetGraphFilePath(clsA);
        string generatedPath = Path.Combine(Path.GetDirectoryName(graphPath) ?? "", Path.GetFileNameWithoutExtension(graphPath) + ".g.cs");
        Assert.Equal(new[] { graphPath, generatedPath }, result.WrittenFiles);
        Assert.False(clsA.IsDirty);
        Assert.False(clsB.IsDirty);
    }

    [Fact]
    public void AddingThenUndoingANodeLeavesTheClassDirty()
    {
        ExecutionGraph graph = vmA.Methods.Single().Graph;
        Node? added = null;
        var command = new DelegateUndoableCommand("Add Node",
            execute: () => added = new LiteralNode(graph, TypeSpecifier.FromType<int>()),
            undo: () =>
            {
                if (added is { } node)
                {
                    graph.Nodes.Remove(node);
                }
            });

        vmA.UndoRedo.Do(command);
        Assert.True(clsA.IsDirty);

        vmA.UndoRedo.Undo();
        Assert.True(clsA.IsDirty, "Undo re-applies a command (editor-services.md §3), so the class stays dirty.");
    }

    [Fact]
    public async Task UndoingBackToTheLoadedContentRewritesNothing()
    {
        ExecutionGraph graph = vmA.Methods.Single().Graph;
        Node? added = null;
        vmA.UndoRedo.Do(new DelegateUndoableCommand("Add Node",
            execute: () => added = new LiteralNode(graph, TypeSpecifier.FromType<int>()),
            undo: () =>
            {
                if (added is { } node)
                {
                    graph.Nodes.Remove(node);
                }
            }));
        vmA.UndoRedo.Undo();

        var result = await editor.Context.Persistence.SaveAsync(project, _ => "// generated\n", TestContext.Current.CancellationToken);

        // Undo put the graph back to exactly what is on disk, so SaveAsync's byte comparison skips
        // the rewrite even though the class was marked dirty.
        string graphPath = project.GetGraphFilePath(clsA);
        Assert.DoesNotContain(graphPath, result.WrittenFiles);
        Assert.False(clsA.IsDirty);
    }

    [Fact]
    public void RenamingAMethodInTheInspectorMarksDirty()
    {
        vmA.Methods.Single().Name = "Renamed";
        Assert.True(clsA.IsDirty);
    }

    [Fact]
    public void EditingAPinValueMarksDirty()
    {
        var call = vmA.Methods.Single().Graph.Nodes.OfType<CallMethodNode>().Single();

        call.InputDataPins.Single().UnconnectedValue = "Changed";

        Assert.True(clsA.IsDirty);
        Assert.False(clsB.IsDirty);
    }

    [Fact]
    public void DisconnectingAPinMarksDirty()
    {
        var call = vmA.Methods.Single().Graph.Nodes.OfType<CallMethodNode>().Single();

        GraphUtil.DisconnectPin(call.InputExecPins.Single());

        Assert.True(clsA.IsDirty);
    }
}
