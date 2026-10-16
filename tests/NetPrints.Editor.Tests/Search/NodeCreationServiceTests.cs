using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.NodeSearch;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.Tests.Search;

/// <summary>The one node creation service of the node search (FR-016, FR-117, research R10 SUG2).</summary>
public sealed class NodeCreationServiceTests : FixtureGraphTestBase
{
    public NodeCreationServiceTests(IReflectionHost sharedReflection)
        : base(sharedReflection)
    {
    }

    private sealed class FakePrompts : INodeSearchPrompts
    {
        public TypeSpecifier? Type { get; set; }

        public MethodSpecifier? Method { get; set; }

        public int TypeAsked { get; private set; }

        public int MethodAsked { get; private set; }

        public Task<TypeSpecifier?> SelectTypeAsync()
        {
            TypeAsked++;
            return Task.FromResult(Type);
        }

        public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods)
        {
            MethodAsked++;
            return Task.FromResult(Method);
        }
    }

    private NodeCreationService Service(FakePrompts prompts) => new(Graph, prompts);

    private NodeKindEntry KindEntry(string kind)
    {
        var nodeKind = Editor.Context.Extensions.Current.NodeKinds.First(k => k.Kind == kind);
        return new NodeKindEntry("NetPrints", nodeKind.Suggestions[0]);
    }

    [Fact]
    public void ANegativePositionIsClampedToTheCanvas()
    {
        var node = Service(new FakePrompts()).Create(new GraphPoint(-10, -5), null, g => new IfElseNode(g));

        Assert.Equal(0, node.PositionX);
        Assert.Equal(0, node.PositionY);
        Assert.Contains(node, Method.Nodes);
    }

    [Fact]
    public void CreatingForAPinConnectsAndUndoesInOneStep()
    {
        var service = Service(new FakePrompts());
        var exec = Method.EntryNode.ExecPin;

        var wiredBefore = exec.OutgoingPin;

        var node = service.Create(new GraphPoint(10, 20), exec, g => new IfElseNode(g));

        Assert.Contains(node, Method.Nodes);
        Assert.Same(node, exec.OutgoingPin?.Node);
        Assert.True(ClassContext.UndoRedo.Undo());
        Assert.DoesNotContain(node, Method.Nodes);
        Assert.Same(wiredBefore, exec.OutgoingPin);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    private sealed record OwnFactoryEntry() : NodeSearchEntry("Own", "Own factory", "none")
    {
        public override Task<Node?> CreateAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
            Task.FromResult<Node?>(context.Service.Create(position, pin, g => new IfElseNode(g)));
    }

    [Fact]
    public async Task AnEntryOfAnyKindCreatesItsOwnNode()
    {
        var node = await Service(new FakePrompts()).CreateAsync(new OwnFactoryEntry(), new GraphPoint(3, 4), null);

        Assert.IsType<IfElseNode>(node);
        Assert.Contains(node, Method.Nodes);
    }

    [Fact]
    public async Task ATypeEntryCreatesATypeNodeWithoutPrompting()
    {
        var prompts = new FakePrompts();

        var node = await Service(prompts).CreateAsync(new TypeEntry("Types", StringTypeSpecifier), new GraphPoint(5, 6), null);

        var typeNode = Assert.IsType<TypeNode>(node);
        Assert.Equal(StringTypeSpecifier, typeNode.Type);
        Assert.Equal(0, prompts.TypeAsked);
    }

    [Fact]
    public async Task TheLiteralKindAsksForATypeThroughThePrompts()
    {
        var prompts = new FakePrompts { Type = IntType };

        var node = await Service(prompts).CreateAsync(KindEntry(BuiltInNodeKinds.Literal), new GraphPoint(5, 6), null);

        Assert.IsType<LiteralNode>(node);
        Assert.Equal(1, prompts.TypeAsked);
    }

    [Fact]
    public async Task CancellingThePromptCreatesNothing()
    {
        var prompts = new FakePrompts();
        int before = Method.Nodes.Count;

        var node = await Service(prompts).CreateAsync(KindEntry(BuiltInNodeKinds.Literal), new GraphPoint(5, 6), null);

        Assert.Null(node);
        Assert.Equal(before, Method.Nodes.Count);
        Assert.False(ClassContext.UndoRedo.CanUndo);
    }

    [Fact]
    public async Task MakeDelegateAsksForAMethodThroughThePrompts()
    {
        var method = new MethodSpecifier("Invoke", [], [], MethodModifiers.None, MemberVisibility.Public, StringTypeSpecifier, []);
        var prompts = new FakePrompts { Method = method };
        var entry = new MakeDelegateEntry("Delegates", new MakeDelegateTypeInfo(StringTypeSpecifier, StringTypeSpecifier));

        var node = await Service(prompts).CreateAsync(entry, new GraphPoint(1, 1), null);

        Assert.IsType<MakeDelegateNode>(node);
        Assert.Equal(1, prompts.MethodAsked);
    }

    private static TypeSpecifier StringTypeSpecifier => TypeSpecifier.FromType<string>();
}
