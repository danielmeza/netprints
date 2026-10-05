using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Inspectors;

public sealed class InspectorGraphSelectionTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();
    private readonly List<NodeGraphViewModel> graphs = [];

    public async ValueTask DisposeAsync()
    {
        graphs.ForEach(graph => graph.Dispose());
        await rig.DisposeAsync();
    }

    private (ProjectSessionViewModel Session, ClassGraph Class, MethodGraph Method, NodeGraphViewModel Graph) OpenMethodDocument(ProjectSessionViewModel session)
    {
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        var graph = new NodeGraphViewModel(method, session.ContextFor(cls).Services);
        graphs.Add(graph);
        var document = new GraphDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), graph, cls, session);
        rig.Shell.ActiveDocument = rig.Shell.AddDocument(document);
        return (session, cls, method, graph);
    }

    private static void Select(NodeGraphViewModel graph, Node? node)
    {
        foreach (NodeViewModel item in graph.Nodes)
        {
            item.IsSelected = ReferenceEquals(item.Node, node);
        }
    }

    [Fact]
    public async Task SelectingAVariableGetterNodeShowsItsVariable()
    {
        var (session, cls, method, graph) = OpenMethodDocument(await rig.OpenSessionAsync());
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        Variable variable = cls.Variables[0];
        var getter = new VariableGetterNode(method, variable.Specifier);

        Select(graph, getter);

        Assert.Same(variable, Assert.IsType<MemberVariableViewModel>(rig.Inspector.Content).Variable);
    }

    [Fact]
    public async Task SelectingACallToAMethodOfTheProjectShowsThatMethod()
    {
        var (session, cls, method, graph) = OpenMethodDocument(await rig.OpenSessionAsync());
        var callee = new MethodGraph("Callee") { Class = cls };
        cls.Methods.Add(callee);
        MethodViewModel calleeViewModel = session.ContextFor(cls).Methods.Single(item => ReferenceEquals(item.Graph, callee));
        var call = new CallMethodNode(method, calleeViewModel.ToMethodSpecifier(cls.Type));

        Select(graph, call);

        Assert.Same(callee, Assert.IsType<MethodViewModel>(rig.Inspector.Content).Graph);
    }

    [Fact]
    public async Task AnyOtherNodeOrNoNodeShowsTheMemberThatOwnsTheGraph()
    {
        var (session, cls, method, graph) = OpenMethodDocument(await rig.OpenSessionAsync());
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var getter = new VariableGetterNode(method, cls.Variables[0].Specifier);
        var other = new IfElseNode(method);
        Select(graph, getter);
        Assert.IsType<MemberVariableViewModel>(rig.Inspector.Content);

        Select(graph, other);
        Assert.Same(method, Assert.IsType<MethodViewModel>(rig.Inspector.Content).Graph);

        Select(graph, getter);
        Select(graph, null);
        Assert.Same(method, Assert.IsType<MethodViewModel>(rig.Inspector.Content).Graph);
    }

    [Fact]
    public async Task TheMostRecentSelectionWinsWhetherItCameFromTheTreeOrTheGraph()
    {
        var (session, cls, method, graph) = OpenMethodDocument(await rig.OpenSessionAsync());
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var getter = new VariableGetterNode(method, cls.Variables[0].Specifier);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.IsType<ClassInspectorViewModel>(rig.Inspector.Content);

        Select(graph, getter);
        Assert.IsType<MemberVariableViewModel>(rig.Inspector.Content);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.IsType<ClassInspectorViewModel>(rig.Inspector.Content);
    }

    [Fact]
    public async Task ANodeSelectedInAGraphThatIsNoLongerActiveDoesNotDriveTheInspector()
    {
        var (session, cls, method, graph) = OpenMethodDocument(await rig.OpenSessionAsync());
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var getter = new VariableGetterNode(method, cls.Variables[0].Specifier);
        rig.Shell.ActiveDocument = null;
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);

        Select(graph, getter);

        Assert.IsType<ClassInspectorViewModel>(rig.Inspector.Content);
    }
}
