using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Pins;

/// <summary>An unconnected Target pin shows "self" where the generated code writes <c>this</c> (FR-096, T091b).</summary>
public class TargetPinSelfTests(TestEditor editor) : GraphTestBase(editor)
{
    public static TheoryData<string> MemberKinds() => ["getter", "setter", "call", "delegate"];

    private Node Create(string kind, TypeSpecifier declaringType, bool isStatic = false)
    {
        var variableModifiers = isStatic ? VariableModifiers.Static : VariableModifiers.None;
        var methodModifiers = isStatic ? MethodModifiers.Static : MethodModifiers.None;
        var variable = new VariableSpecifier("Field", IntType, MemberVisibility.Public, MemberVisibility.Public, declaringType, variableModifiers);
        var method = new MethodSpecifier("Member", [], [], methodModifiers, MemberVisibility.Public, declaringType, []);

        return kind switch
        {
            "getter" => new VariableGetterNode(Method, variable),
            "setter" => new VariableSetterNode(Method, variable),
            "call" => new CallMethodNode(Method, method),
            "delegate" => new MakeDelegateNode(Method, method),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private NodePinViewModel TargetOf(Node node) => VmOf(node switch
    {
        VariableNode variable => variable.TargetPin ?? throw new InvalidOperationException("No target pin."),
        CallMethodNode call => call.TargetPin,
        MakeDelegateNode makeDelegate => makeDelegate.TargetPin,
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    });

    private void Connect(Node node, NodeOutputDataPin source)
    {
        var target = TargetOf(node);
        GraphUtil.ConnectNodePins(source, (NodeInputDataPin)target.Pin);
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void AMemberOfTheGraphsClassShowsSelf(string kind)
    {
        Assert.Equal("self", TargetOf(Create(kind, Class.Type)).SelfHint);
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void AMemberOfABaseTypeShowsSelf(string kind)
    {
        Assert.Equal("self", TargetOf(Create(kind, TypeSpecifier.FromType<object>())).SelfHint);
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void AMemberOfAnUnrelatedTypeShowsNothing(string kind)
    {
        Assert.Null(TargetOf(Create(kind, StringType)).SelfHint);
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void AStaticMethodGraphShowsNothingAndFollowsTheModifier(string kind)
    {
        var target = TargetOf(Create(kind, Class.Type));
        Assert.Equal("self", target.SelfHint);

        Method.Modifiers = MethodModifiers.Static;
        Assert.Null(target.SelfHint);

        Method.Modifiers = MethodModifiers.None;
        Assert.Equal("self", target.SelfHint);
    }

    [Theory]
    [InlineData("getter")]
    [InlineData("setter")]
    [InlineData("call")]
    public void AStaticMemberHasNoTargetPinAndNoPinShowsSelf(string kind)
    {
        var node = VmOf(Create(kind, Class.Type, isStatic: true));

        Assert.All(node.AllPins, pin => Assert.Null(pin.SelfHint));
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void OnlyTheTargetPinShowsSelf(string kind)
    {
        var node = Create(kind, Class.Type);
        var target = TargetOf(node);

        Assert.All(VmOf(node).AllPins.Where(pin => pin != target), pin => Assert.Null(pin.SelfHint));
    }

    [Theory]
    [MemberData(nameof(MemberKinds))]
    public void ConnectingAWireHidesSelfAndDisconnectingShowsItAgain(string kind)
    {
        var node = Create(kind, Class.Type);
        var target = TargetOf(node);
        var other = new VariableGetterNode(Method, new VariableSpecifier("Other", Class.Type, MemberVisibility.Public, MemberVisibility.Public, Class.Type, VariableModifiers.None));
        var changed = new List<string?>();
        target.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Connect(node, other.ValuePin);
        Assert.Null(target.SelfHint);
        Assert.Contains(nameof(NodePinViewModel.SelfHint), changed);

        GraphUtil.DisconnectPin(target.Pin);
        Assert.Equal("self", target.SelfHint);
    }

    [Fact]
    public async Task TheTextFollowsUndoAndRedoOfDeletingTheWiresSource()
    {
        var node = Create("call", Class.Type);
        var target = TargetOf(node);
        var other = new VariableGetterNode(Method, new VariableSpecifier("Other", Class.Type, MemberVisibility.Public, MemberVisibility.Public, Class.Type, VariableModifiers.None));
        Assert.True(Graph.Connect(VmOf(other.ValuePin), target));
        Assert.Null(target.SelfHint);
        ClassContext.UndoRedo.Clear();

        Graph.SelectNodes([VmOf(other)], deselectPrevious: true);
        var context = new CommandContext(new FakeShell(), null, null, Graph, new CommandSelection([.. Graph.SelectedNodes]));
        await new DeleteCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal("self", target.SelfHint);

        ClassContext.UndoRedo.Undo();
        Assert.Null(target.SelfHint);

        ClassContext.UndoRedo.Redo();
        Assert.Equal("self", target.SelfHint);
    }

    [Fact]
    public void TheTextIsOneNamedConstant()
    {
        Assert.Equal("self", NodePinViewModel.SelfText);
    }
}
