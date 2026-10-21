using NetPrints.Core;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

public class NodeCapabilityTests(TestEditor editor) : GraphTestBase(editor)
{
    private const string AddLabel = "Add stub pin";
    private const string RemoveLabel = "Remove stub pin";

    private sealed class ListStub : Node, IPinListEditor
    {
        public ListStub(NodeGraph graph)
            : base(graph)
        {
        }

        public int Added { get; private set; }

        public int Removed { get; private set; }

        bool IPinListEditor.ShowsPinButtons => true;

        string IPinListEditor.AddPinLabel => AddLabel;

        string IPinListEditor.RemovePinLabel => RemoveLabel;

        bool IPinListEditor.CanRemovePin => Added > Removed;

        bool IPinListEditor.ChangesMethodSignature => false;

        IReadOnlyList<int>? IPinListEditor.SlotsAfterAdd => null;

        IReadOnlyList<int>? IPinListEditor.SlotsAfterRemove => null;

        void IPinListEditor.AddPin() => Added++;

        void IPinListEditor.RemovePin() => Removed++;
    }

    private sealed class NameStub : Node, IPinNameEditor
    {
        public NameStub(NodeGraph graph)
            : base(graph)
        {
            AddOutputDataPin("Editable", TypeSpecifier.FromType<int>());
            AddOutputDataPin("Fixed", TypeSpecifier.FromType<int>());
        }

        bool IPinNameEditor.CanRenamePin(NodePin pin) => pin.Name == "Editable";
    }

    private sealed class OverloadStub : Node, IOverloadableNode
    {
        public OverloadStub(NodeGraph graph, string overload)
            : base(graph)
        {
            CurrentOverload = overload;
        }

        public object CurrentOverload { get; }

        public Node? ChangeOverload(object overload) => new OverloadStub(Graph, (string)overload);
    }

    private sealed class ReferenceStub(NodeGraph graph, MemberKey key) : Node(graph), IMemberReferencingNode
    {
        public bool RefersTo(MemberKey member) => member.Equals(key);

        public Action Retarget(MemberKey member, string newName) => () => { };
    }

    [Fact]
    public void ABuiltInNodeDeclaresTheRolesItPlays()
    {
        Assert.All<Node>([Method.EntryNode, Method.MainReturnNode, new MakeArrayNode(Method)], node => Assert.IsAssignableFrom<IPinListEditor>(node));
        Assert.IsAssignableFrom<IPinListEditor>(new ClassGraph().ReturnNode);
        Assert.All<Node>([new CallMethodNode(Method, ConsoleWriteLine(StringType)), new MakeArrayNode(Method)], node => Assert.IsAssignableFrom<IOverloadableNode>(node));
        Assert.All<Node>([Method.EntryNode, Method.MainReturnNode], node => Assert.IsAssignableFrom<IPinNameEditor>(node));

        Node ifElse = new IfElseNode(Method);
        Assert.False(ifElse is IPinListEditor or IOverloadableNode or IPinNameEditor or IMemberReferencingNode);
    }

    [Fact]
    public void ThePinButtonsFollowThePinListRoleNotTheNodeType()
    {
        var stub = new ListStub(Method);
        var view = VmOf(stub);

        Assert.True(view.ShowLeftPinButtons);
        Assert.Equal(AddLabel, view.LeftPlusToolTip);
        Assert.Equal(RemoveLabel, view.LeftMinusToolTip);

        view.LeftPinsPlusCommand.Execute(null);
        view.LeftPinsMinusCommand.Execute(null);
        view.LeftPinsMinusCommand.Execute(null);

        Assert.Equal(1, stub.Added);
        Assert.Equal(1, stub.Removed);
    }

    [Fact]
    public void ANodeWithoutThePinListRoleHasNoPinButtons()
    {
        Assert.False(VmOf(new IfElseNode(Method)).ShowLeftPinButtons);
    }

    [Fact]
    public void ThePinNameIsEditableWhenTheNodeSaysSo()
    {
        var stub = new NameStub(Method);

        Assert.True(VmOf(stub.OutputDataPins[0]).IsNameEditable);
        Assert.False(VmOf(stub.OutputDataPins[1]).IsNameEditable);
    }

    [Fact]
    public void ChangingTheOverloadOfAnOverloadableNodeReplacesIt()
    {
        var stub = new OverloadStub(Method, "first");

        Node replacement = ModelOperations.ChangeOverload(stub, "second");

        Assert.NotSame(stub, replacement);
        Assert.Equal("second", ModelOperations.GetCurrentOverload(replacement));
        Assert.Equal("first", ModelOperations.GetCurrentOverload(stub));
    }

    [Fact]
    public void ANodeThatSwitchesInPlaceKeepsItsIdentity()
    {
        var array = new MakeArrayNode(Method);

        Node result = ModelOperations.ChangeOverload(array, ModelOperations.UseInitializerList);

        Assert.Same(array, result);
        Assert.False(array.UsePredefinedSize);
    }

    [Fact]
    public void ANodeThatIsNotOverloadableRefusesTheChange()
    {
        Assert.Throws<InvalidOperationException>(() => ModelOperations.ChangeOverload(new IfElseNode(Method), "x"));
    }

    [Fact]
    public void TheInspectorShowsTheMemberAnyReferencingNodeRefersTo()
    {
        var project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        var cls = new ClassGraph { Name = "C", Namespace = "N", Project = project };
        project.Classes.Add(cls);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var method = new MethodGraph("Run") { Class = cls };
        cls.Methods.Add(method);
        var key = new MemberKey(MemberKind.Variable, cls.Type, "count", []);

        Assert.Same(cls.Variables[0], GraphSelectionInspectorTarget.Resolve(method, [new ReferenceStub(method, key)]));
    }
}
