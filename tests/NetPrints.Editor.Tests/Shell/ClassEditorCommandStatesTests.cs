using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Main;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>The class editor's invoker re-queries on one pulse that covers selection, open graph, undo history and session.</summary>
public sealed class ClassEditorCommandStatesTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<MainEditorViewModel> models = [];

    public async ValueTask DisposeAsync()
    {
        models.ForEach(model => model.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<(MainEditorViewModel Model, ClassEditorViewModel ClassEditor, CommandInvoker Invoker)> OpenAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var model = new MainEditorViewModel(editor.Context);
        models.Add(model);
        await model.LoadProjectAsync(path);
        ClassGraph cls = Assert.IsType<Project>(model.Project).Classes.Single();
        model.OpenClassCommand.Execute(cls);
        ClassEditorViewModel classEditor = Assert.IsType<ClassEditorViewModel>(editor.Windows.FindClassEditor(cls));
        return (model, classEditor, Assert.IsType<CommandInvoker>(classEditor.Commands));
    }

    [Fact]
    public async Task OpeningAGraphAndChangingItsSelectionPulse()
    {
        (_, ClassEditorViewModel classEditor, CommandInvoker invoker) = await OpenAsync();
        int pulses = 0;
        invoker.CommandStatesChanged += (_, _) => pulses++;

        classEditor.CreateMethodCommand.Execute(null);
        Assert.True(pulses >= 1, "opening a graph pulses");
        NodeGraphViewModel graph = Assert.IsType<NodeGraphViewModel>(classEditor.OpenedGraph);
        Node added = graph.AddNode<IfElseNode>(new GraphPoint(200, 100));
        NodeViewModel node = graph.Nodes.Single(vm => vm.Node == added);
        int before = pulses;

        graph.SelectNodes([node], deselectPrevious: true);

        Assert.True(pulses > before, "selecting a node pulses");
    }

    [Fact]
    public async Task AnEditToTheClassHistoryPulses()
    {
        (_, ClassEditorViewModel classEditor, CommandInvoker invoker) = await OpenAsync();
        int pulses = 0;
        invoker.CommandStatesChanged += (_, _) => pulses++;

        classEditor.UndoRedo.Do(new DelegateUndoableCommand("edit", () => { }, () => { }));

        Assert.True(pulses >= 1);
    }

    [Fact]
    public async Task ReplacingTheSessionPulses()
    {
        (MainEditorViewModel model, _, CommandInvoker invoker) = await OpenAsync();
        int pulses = 0;
        invoker.CommandStatesChanged += (_, _) => pulses++;

        await ((IProjectActions)model).CloseProjectAsync(TestContext.Current.CancellationToken);

        Assert.True(pulses >= 1);
    }

    [Fact]
    public async Task AnUnsubscribedInvokerStopsWatchingTheEditor()
    {
        (_, ClassEditorViewModel classEditor, CommandInvoker invoker) = await OpenAsync();
        int pulses = 0;
        EventHandler handler = (_, _) => pulses++;
        invoker.CommandStatesChanged += handler;
        invoker.CommandStatesChanged -= handler;

        classEditor.CreateMethodCommand.Execute(null);
        classEditor.UndoRedo.Do(new DelegateUndoableCommand("edit", () => { }, () => { }));

        Assert.Equal(0, pulses);
    }
}
