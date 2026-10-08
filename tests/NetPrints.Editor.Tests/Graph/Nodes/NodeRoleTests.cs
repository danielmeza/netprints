using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Nodes;

/// <summary>The node-header role of every node kind (FR-086, T091a).</summary>
public class NodeRoleTests(TestEditor editor) : GraphTestBase(editor)
{
    private static readonly (NodeVisualKind Kind, bool Pure, string Role)[] Rows =
    [
        (NodeVisualKind.Entry, false, "Entry"),
        (NodeVisualKind.Entry, true, "Entry"),
        (NodeVisualKind.Return, false, "Entry"),
        (NodeVisualKind.Return, true, "Entry"),
        (NodeVisualKind.CallMethod, false, "Call"),
        (NodeVisualKind.CallMethod, true, "Pure"),
        (NodeVisualKind.CallStatic, false, "Call"),
        (NodeVisualKind.CallStatic, true, "Pure"),
        (NodeVisualKind.Constructor, false, "Constructor"),
        (NodeVisualKind.Constructor, true, "Constructor"),
        (NodeVisualKind.MakeDelegate, false, "Pure"),
        (NodeVisualKind.MakeDelegate, true, "Pure"),
        (NodeVisualKind.Type, false, "Pure"),
        (NodeVisualKind.Type, true, "Pure"),
        (NodeVisualKind.VariableGetter, false, "Variable"),
        (NodeVisualKind.VariableGetter, true, "Variable"),
        (NodeVisualKind.VariableSetter, false, "Variable"),
        (NodeVisualKind.VariableSetter, true, "Variable"),
        (NodeVisualKind.MakeArray, false, "Pure"),
        (NodeVisualKind.MakeArray, true, "Pure"),
        (NodeVisualKind.Throw, false, "Throw"),
        (NodeVisualKind.Throw, true, "Throw"),
        (NodeVisualKind.Ternary, false, "Pure"),
        (NodeVisualKind.Ternary, true, "Pure"),
        (NodeVisualKind.Default, false, "Flow"),
        (NodeVisualKind.Default, true, "Pure"),
    ];

    public static TheoryData<NodeVisualKind, bool, string> Table()
    {
        TheoryData<NodeVisualKind, bool, string> data = [];
        foreach (var (kind, pure, role) in Rows)
        {
            data.Add(kind, pure, role);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void EveryVisualKindGetsOneRole(NodeVisualKind kind, bool pure, string expected)
    {
        Assert.Equal(expected, NodeRole.Resolve(kind, pure, hasExecPins: !pure, returnsTask: false).Name);
    }

    [Fact]
    public void TheTableCoversEveryVisualKindPureAndImpure()
    {
        var covered = Rows.Select(row => (row.Kind, row.Pure)).ToHashSet();

        foreach (var kind in Enum.GetValues<NodeVisualKind>())
        {
            Assert.Contains((kind, false), covered);
            Assert.Contains((kind, true), covered);
        }
    }

    [Theory]
    [InlineData(NodeVisualKind.CallMethod)]
    [InlineData(NodeVisualKind.CallStatic)]
    public void AnImpureCallToAMethodReturningATaskIsAsync(NodeVisualKind kind)
    {
        Assert.Equal("Async", NodeRole.Resolve(kind, isPure: false, hasExecPins: true, returnsTask: true).Name);
    }

    [Theory]
    [InlineData(1000, true, "Flow")]
    [InlineData(1000, false, "Pure")]
    public void AKindTheTableDoesNotListGetsItsRoleByConvention(int kind, bool hasExecPins, string expected)
    {
        var role = NodeRole.Resolve((NodeVisualKind)kind, isPure: !hasExecPins, hasExecPins, returnsTask: false);

        Assert.Equal(expected, role.Name);
        Assert.NotEqual("Default", role.Name);
    }

    [Fact]
    public void ARoleHasAStyleClassAndEveryRoleIsListed()
    {
        Assert.Equal(["Entry", "Call", "Pure", "Flow", "Variable", "Constructor", "Async", "Throw"], NodeRole.All.Select(r => r.Name));
        Assert.All(NodeRole.All, role => Assert.Equal("role-" + role.Name.ToLowerInvariant(), role.StyleClass));
    }

    [Fact]
    public void ViewModelsTakeTheirRoleFromTheNode()
    {
        var toUpper = FindMethod(typeof(string), "ToUpperInvariant");
        var delay = new MethodSpecifier("DelayAsync", [new MethodParameter("ms", IntType, MethodParameterPassType.Default, false, null)],
            [TypeSpecifier.FromType<Task>()], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Task)), []);
        var genericTask = new MethodSpecifier("ReadAsync", [], [TypeSpecifier.FromType<Task<int>>()], MethodModifiers.None,
            MemberVisibility.Public, TypeSpecifier.FromType(typeof(Task)), []);
        var valueTask = new MethodSpecifier("ReadAsync", [], [TypeSpecifier.FromType<ValueTask<int>>()], MethodModifiers.None,
            MemberVisibility.Public, TypeSpecifier.FromType(typeof(Task)), []);
        var variable = new VariableSpecifier("Length", IntType, MemberVisibility.Public, MemberVisibility.Public, StringType, VariableModifiers.None);

        Assert.Equal("Entry", VmOf(Method.EntryNode).Role.Name);
        Assert.Equal("Entry", VmOf(Method.MainReturnNode).Role.Name);
        Assert.Equal("Call", VmOf(new CallMethodNode(Method, toUpper)).Role.Name);
        Assert.Equal("Async", VmOf(new CallMethodNode(Method, delay)).Role.Name);
        Assert.Equal("Async", VmOf(new CallMethodNode(Method, genericTask)).Role.Name);
        Assert.Equal("Async", VmOf(new CallMethodNode(Method, valueTask)).Role.Name);
        Assert.Equal("Variable", VmOf(new VariableGetterNode(Method, variable)).Role.Name);
        Assert.Equal("Pure", VmOf(new TernaryNode(Method)).Role.Name);
        Assert.Equal("Flow", VmOf(new IfElseNode(Method)).Role.Name);
        Assert.Equal("Throw", VmOf(new ThrowNode(Method)).Role.Name);
    }

    [Fact]
    public void ThePurityToggleMovesACallBetweenCallAndPure()
    {
        var toUpper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        var viewModel = VmOf(toUpper);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IsPure = true;

        Assert.Equal("Pure", viewModel.Role.Name);
        Assert.Contains(nameof(NodeViewModel.Role), changed);
        viewModel.IsPure = false;
        Assert.Equal("Call", viewModel.Role.Name);
    }

    [Fact]
    public void ANodeOfAnUnknownTypeGetsItsRoleByConvention()
    {
        var withExec = VmOf(new UnknownNode(Method, withExecPins: true));
        var withoutExec = VmOf(new UnknownNode(Method, withExecPins: false));

        Assert.Equal(NodeVisualKind.Default, withExec.VisualKind);
        Assert.Equal("Flow", withExec.Role.Name);
        Assert.Equal("Pure", withoutExec.Role.Name);
    }

    [Theory]
    [InlineData(NodeVisualKind.Default, IconIds.NodeKindDefault)]
    [InlineData(NodeVisualKind.Entry, IconIds.NodeKindEntry)]
    [InlineData(NodeVisualKind.Return, IconIds.NodeKindReturn)]
    [InlineData(NodeVisualKind.CallMethod, IconIds.NodeKindCallMethod)]
    [InlineData(NodeVisualKind.CallStatic, IconIds.NodeKindCallStatic)]
    [InlineData(NodeVisualKind.Constructor, IconIds.NodeKindConstructor)]
    [InlineData(NodeVisualKind.MakeDelegate, IconIds.NodeKindMakeDelegate)]
    [InlineData(NodeVisualKind.Type, IconIds.NodeKindType)]
    [InlineData(NodeVisualKind.VariableGetter, IconIds.NodeKindVariableGetter)]
    [InlineData(NodeVisualKind.VariableSetter, IconIds.NodeKindVariableSetter)]
    [InlineData(NodeVisualKind.MakeArray, IconIds.NodeKindMakeArray)]
    [InlineData(NodeVisualKind.Throw, IconIds.NodeKindThrow)]
    [InlineData(NodeVisualKind.Ternary, IconIds.NodeKindTernary)]
    public void EveryKindHasItsGlyph(NodeVisualKind kind, string iconId)
    {
        Assert.Equal(iconId, NodeIcons.For(kind));
        Assert.True(IconRegistry.Shared.IsKnown(iconId));
    }

    [Fact]
    public void EveryVisualKindHasItsOwnGlyph()
    {
        Assert.Equal(Enum.GetValues<NodeVisualKind>().Length, Enum.GetValues<NodeVisualKind>().Select(NodeIcons.For).Distinct().Count());
    }

    private sealed class UnknownNode : Node
    {
        public UnknownNode(NodeGraph graph, bool withExecPins)
            : base(graph)
        {
            if (withExecPins)
            {
                AddInputExecPin("Exec");
                AddOutputExecPin("Then");
            }
        }
    }
}
