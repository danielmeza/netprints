using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Commands;

public sealed class EditCommandsTests : SessionCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NodeGraphViewModel OpenGraph()
    {
        var classContext = Track(new ClassContext(new ClassGraph { Name = "C", Namespace = "N" }, Editor.Context, new UndoRedoStack()));
        return Track(new NodeGraphViewModel(classContext.CreateMethod(), classContext.Services));
    }

    private static NodeViewModel AddDeletableNode(NodeGraphViewModel graph)
    {
        Node added = graph.AddNode<IfElseNode>(new GraphPoint(200, 100));
        return graph.Nodes.Single(vm => vm.Node == added);
    }

    private CommandContext ContextOf(ProjectSessionViewModel session, ClassGraph? activeClass = null, object? treeItem = null, CommandScope scope = CommandScope.Global)
    {
        if (activeClass is not null)
        {
            Shell.OpenDocument(DocumentId.Graph(session.ClassPathOf(activeClass), "class"));
        }

        return Shell.Context(session: session, selection: treeItem is null ? null : new CommandSelection([], treeItem), scope: scope);
    }

    private static List<string> Push(UndoRedoStack stack, string name, List<string> log)
    {
        stack.Do(new DelegateUndoableCommand(name, () => log.Add("do " + name), () => log.Add("undo " + name)));
        return log;
    }

    public static TheoryData<ICommandHandler> HandlersNeedingAnOpenProject() =>
        [new UndoCommandHandler(), new RedoCommandHandler(), new RenameCommandHandler(), new DeleteCommandHandler()];

    public static TheoryData<ICommandHandler> HandlersNeedingAGraph() =>
        [new SelectAllCommandHandler(), new CancelCommandHandler(), new NodeSearchCommandHandler(), new FrameSelectionCommandHandler(), new FitAllCommandHandler()];

    [Theory]
    [MemberData(nameof(HandlersNeedingAnOpenProject))]
    public void NothingRunsWithoutAProjectOrASelection(ICommandHandler handler) =>
        Assert.False(handler.CanExecute(Shell.Context()));

    [Theory]
    [MemberData(nameof(HandlersNeedingAGraph))]
    public void CanvasCommandsNeedAnActiveGraph(ICommandHandler handler) =>
        Assert.False(handler.CanExecute(Shell.Context()));

    [Fact]
    public async Task UndoAndRedoActOnTheActiveDocumentsClassAndNameTheAction()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        List<string> log = Push(session.UndoStackFor(cls), "Add node", []);
        CommandContext context = ContextOf(session, cls);
        var undo = new UndoCommandHandler();
        var redo = new RedoCommandHandler();

        Assert.True(undo.CanExecute(context));
        Assert.Equal("Undo Add node", undo.DynamicLabel(context));
        Assert.False(redo.CanExecute(context));
        Assert.Equal("Redo", redo.DynamicLabel(context));

        await undo.ExecuteAsync(context, Token);

        Assert.Equal(["do Add node", "undo Add node"], log);
        Assert.False(undo.CanExecute(context));
        Assert.Equal("Undo", undo.DynamicLabel(context));
        Assert.True(redo.CanExecute(context));
        Assert.Equal("Redo Add node", redo.DynamicLabel(context));

        await redo.ExecuteAsync(context, Token);

        Assert.Equal(["do Add node", "undo Add node", "do Add node"], log);
        Assert.True(undo.CanExecute(context));
    }

    [Fact]
    public async Task UndoActsOnTheTreeSelectionsClassWhenNoDocumentIsActive()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        List<string> log = Push(session.UndoStackFor(cls), "Rename", []);
        CommandContext context = ContextOf(session, activeClass: null, treeItem: cls);

        Assert.Equal("Undo Rename", new UndoCommandHandler().DynamicLabel(context));
        await new UndoCommandHandler().ExecuteAsync(context, Token);

        Assert.Equal(["do Rename", "undo Rename"], log);
    }

    [Fact]
    public async Task UndoPrefersTheActiveDocumentOverTheTreeSelection()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph active = session.Project.Classes.Single();
        ClassGraph other = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        Push(session.UndoStackFor(active), "In active", []);
        Push(session.UndoStackFor(other), "In other", []);
        CommandContext context = ContextOf(session, active, other);

        Assert.Equal("Undo In active", new UndoCommandHandler().DynamicLabel(context));
    }

    [Fact]
    public void UndoWithoutAProjectIsPlainAndDisabled()
    {
        CommandContext context = Shell.Context();

        Assert.Equal("Undo", new UndoCommandHandler().DynamicLabel(context));
        Assert.Equal("Redo", new RedoCommandHandler().DynamicLabel(context));
    }

    [Fact]
    public async Task DeleteRemovesTheSelectedNodesOfTheActiveGraph()
    {
        NodeGraphViewModel graph = OpenGraph();
        NodeViewModel node = AddDeletableNode(graph);
        var handler = new DeleteCommandHandler();
        CommandContext none = Shell.Context(graph: graph, scope: CommandScope.Graph);
        graph.SelectNodes([node], deselectPrevious: true);
        CommandContext selected = Shell.Context(graph: graph, selection: new CommandSelection([node]), scope: CommandScope.Graph);

        Assert.False(handler.CanExecute(none));
        Assert.True(handler.CanExecute(selected));
        await handler.ExecuteAsync(selected, Token);

        Assert.DoesNotContain(graph.Nodes, vm => vm.Node == node.Node);
    }

    [Fact]
    public async Task DeleteAndRenameActOnTheSelectedTreeItem()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        CommandContext context = ContextOf(session, activeClass: null, treeItem: cls, scope: CommandScope.ProjectTree);

        await new DeleteCommandHandler().ExecuteAsync(context, Token);
        await new RenameCommandHandler().ExecuteAsync(context, Token);

        Assert.Equal(["DeleteItem", "RenameItem"], Shell.Project.Calls);
        Assert.Same(cls, Shell.Project.LastItem);
    }

    [Fact]
    public async Task DeleteInTheGraphScopeNeverFallsBackToTheTreeSelection()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        NodeGraphViewModel graph = OpenGraph();
        var handler = new DeleteCommandHandler();
        var treeRow = new MethodGraph("Stale");
        var selection = new CommandSelection([], treeRow);

        CommandContext inTheCanvas = Shell.Context(session: session, graph: graph, selection: selection, scope: CommandScope.Graph);
        CommandContext fromAMenu = Shell.Context(session: session, graph: graph, selection: selection);

        Assert.False(handler.CanExecute(inTheCanvas));
        Assert.False(handler.CanExecute(fromAMenu));
        await handler.ExecuteAsync(inTheCanvas, Token);
        Assert.Empty(Shell.Project.Calls);
    }

    [Fact]
    public async Task DeleteInTheTreeScopeActsOnTheTreeItemEvenWhenNodesAreSelected()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        NodeGraphViewModel graph = OpenGraph();
        NodeViewModel node = AddDeletableNode(graph);
        graph.SelectNodes([node], deselectPrevious: true);
        CommandContext context = Shell.Context(session: session, graph: graph, selection: new CommandSelection([node], cls), scope: CommandScope.ProjectTree);

        await new DeleteCommandHandler().ExecuteAsync(context, Token);

        Assert.Same(cls, Shell.Project.LastItem);
        Assert.Contains(graph.Nodes, vm => vm.Node == node.Node);
    }

    [Fact]
    public async Task RenameInTheGraphScopeTargetsTheActiveGraphAndNotAStaleTreeRow()
    {
        NodeGraphViewModel graph = OpenGraph();
        var staleRow = new MethodGraph("Stale");
        var handler = new RenameCommandHandler();
        CommandContext context = Shell.Context(graph: graph, selection: new CommandSelection([], staleRow), scope: CommandScope.Graph);

        await handler.ExecuteAsync(context, Token);

        Assert.Same(graph.Graph, Shell.Project.LastItem);
    }

    [Fact]
    public async Task RenameInTheGraphScopeOfAnAccessorGraphTargetsItsVariable()
    {
        var classContext = Track(new ClassContext(new ClassGraph { Name = "C", Namespace = "N" }, Editor.Context, new UndoRedoStack()));
        var getter = new MethodGraph("get_V") { Class = classContext.Class };
        var variable = new Variable(classContext.Class, "V", TypeSpecifier.FromType<int>(), getter, null, VariableModifiers.None);
        classContext.Class.Variables.Add(variable);
        var graph = Track(new NodeGraphViewModel(getter, classContext.Services));
        var staleRow = new MethodGraph("Stale");
        CommandContext context = Shell.Context(graph: graph, selection: new CommandSelection([], staleRow), scope: CommandScope.Graph);

        await new RenameCommandHandler().ExecuteAsync(context, Token);

        Assert.Same(variable, Shell.Project.LastItem);
    }

    [Fact]
    public async Task RenameInTheTreeScopeTargetsTheTreeItem()
    {
        NodeGraphViewModel graph = OpenGraph();
        var row = new MethodGraph("Row");
        CommandContext context = Shell.Context(graph: graph, selection: new CommandSelection([], row), scope: CommandScope.ProjectTree);

        await new RenameCommandHandler().ExecuteAsync(context, Token);

        Assert.Same(row, Shell.Project.LastItem);
    }

    [Fact]
    public async Task RenameFallsBackToTheActiveGraphsModel()
    {
        NodeGraphViewModel graph = OpenGraph();
        CommandContext context = Shell.Context(graph: graph);
        var handler = new RenameCommandHandler();

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, Token);

        Assert.Same(graph.Graph, Shell.Project.LastItem);
    }

    [Fact]
    public void RenameIsDisabledForAConstructorTreeItemAndAnActiveConstructorGraph()
    {
        var classContext = Track(new ClassContext(new ClassGraph { Name = "C", Namespace = "N" }, Editor.Context, new UndoRedoStack()));
        var graph = Track(new NodeGraphViewModel(classContext.CreateConstructor(), classContext.Services));
        var handler = new RenameCommandHandler();

        Assert.IsType<ConstructorGraph>(graph.Graph);
        Assert.False(handler.CanExecute(Shell.Context(graph: graph)));
        Assert.False(handler.CanExecute(Shell.Context(selection: new CommandSelection([], new ConstructorGraph()))));
    }

    [Fact]
    public async Task DeleteIsEnabledForVariablesConstructorsMethodsAndEventGraphsOfTheTree()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        var variable = new Variable(cls, "V", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None);
        var handler = new DeleteCommandHandler();

        const CommandScope tree = CommandScope.ProjectTree;
        Assert.True(handler.CanExecute(ContextOf(session, treeItem: variable, scope: tree)));
        Assert.True(handler.CanExecute(ContextOf(session, treeItem: new ConstructorGraph(), scope: tree)));
        Assert.True(handler.CanExecute(ContextOf(session, treeItem: new MethodGraph("M"), scope: tree)));
        Assert.True(handler.CanExecute(ContextOf(session, treeItem: new EventGraph("E"), scope: tree)));
    }

    [Fact]
    public async Task SelectAllSelectsEveryNode()
    {
        NodeGraphViewModel graph = OpenGraph();
        AddDeletableNode(graph);
        CommandContext context = Shell.Context(graph: graph);

        await new SelectAllCommandHandler().ExecuteAsync(context, Token);

        Assert.All(graph.Nodes, node => Assert.True(node.IsSelected));
    }

    [Fact]
    public async Task CancelRunsOnlyWhileAPopupIsOpenAndKeepsTheSelection()
    {
        NodeGraphViewModel graph = OpenGraph();
        NodeViewModel node = AddDeletableNode(graph);
        graph.SelectNodes([node], deselectPrevious: true);
        CommandContext context = Shell.Context(graph: graph);
        var handler = new CancelCommandHandler();
        Assert.False(handler.CanExecute(context), "with no popup open Esc falls through to Nodify");

        graph.Search.IsOpen = true;
        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, Token);

        Assert.False(graph.Search.IsOpen);
        Assert.True(node.IsSelected);
        Assert.False(handler.CanExecute(context));
    }

    [Theory]
    [InlineData(GraphViewRequest.NodeSearch)]
    [InlineData(GraphViewRequest.FrameSelection)]
    [InlineData(GraphViewRequest.FitAll)]
    public async Task ViewportCommandsRaiseARequestOnTheGraph(GraphViewRequest request)
    {
        NodeGraphViewModel graph = OpenGraph();
        NodeViewModel node = AddDeletableNode(graph);
        graph.SelectNodes([node], deselectPrevious: true);
        List<GraphViewRequest> raised = [];
        graph.ViewRequested += (_, r) => raised.Add(r);
        CommandContext context = Shell.Context(graph: graph, selection: new CommandSelection([node]));
        ICommandHandler handler = request switch
        {
            GraphViewRequest.NodeSearch => new NodeSearchCommandHandler(),
            GraphViewRequest.FrameSelection => new FrameSelectionCommandHandler(),
            _ => new FitAllCommandHandler(),
        };

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, Token);

        Assert.Equal([request], raised);
    }

    [Fact]
    public void FrameSelectionNeedsASelection()
    {
        NodeGraphViewModel graph = OpenGraph();
        graph.DeselectNodes();

        Assert.False(new FrameSelectionCommandHandler().CanExecute(Shell.Context(graph: graph)));
    }
}
