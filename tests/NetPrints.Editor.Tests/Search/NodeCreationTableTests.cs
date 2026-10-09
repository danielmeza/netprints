using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// What picking each node search suggestion does (T094a safety net): the node type, the dialogs it asks, what a cancel leaves,
/// the connection made and the undo entry. Over the fixed member set of <see cref="FixtureReflectionHost"/>.
/// </summary>
public sealed class NodeCreationTableTests(IReflectionHost sharedReflection)
    : GraphTestBase(TestEditor.Create(_ => new FixtureReflectionHost(sharedReflection), TestExtensionFolder.CreateHost()))
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly TypeSpecifier ListOfInt = TypeSpecifier.FromType<List<int>>();

    public static IEnumerable<TheoryDataRow<string, int>> BuiltInKinds() =>
        BuiltInNodeLibrary.Instance.NodeKinds
            .Where(kind => kind.Suggestions.Count > 0)
            .Select(kind => new TheoryDataRow<string, int>(kind.Kind, AsksForAType(kind.Kind) ? 1 : 0));

    private static bool AsksForAType(string kind) => kind is BuiltInNodeKinds.Constructor or BuiltInNodeKinds.Literal or BuiltInNodeKinds.Type;

    private static NodeKindDescriptor Descriptor(string kind) => BuiltInNodeLibrary.Instance.NodeKinds.Single(k => k.Kind == kind);

    private async Task<SuggestionItem> OpenAsync(NodeGraphViewModel view, string text, NodePin? pin = null, GraphPoint? position = null)
    {
        await view.OpenSearchAsync(position ?? new GraphPoint(30, 40), pin, Token);
        return view.Search.AllSuggestions.First(item => item.Text == text);
    }

    [Theory(Timeout = 60000)]
    [MemberData(nameof(BuiltInKinds))]
    public async Task EveryBuiltInSuggestionCreatesItsNodeType(string kind, int dialogCalls)
    {
        NodeKindDescriptor descriptor = Descriptor(kind);
        Editor.Dialogs.TypeAnswer = ListOfInt;
        HashSet<Node> before = [.. Method.Nodes];
        SuggestionItem item = await OpenAsync(Graph, descriptor.Suggestions[0].DisplayName);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        Node created = Assert.Single(Method.Nodes.Where(node => !before.Contains(node)));
        Assert.Equal(descriptor.NodeType, created.GetType());
        Assert.Equal(dialogCalls, Editor.Dialogs.SelectTypeCalls.Count);
        Assert.Equal((30, 40), (created.PositionX, created.PositionY));
        Assert.Equal("Add node", ClassContext.UndoRedo.UndoName);
        Assert.False(Graph.Search.IsOpen);
    }

    [Theory(Timeout = 60000)]
    [InlineData(BuiltInNodeKinds.Constructor)]
    [InlineData(BuiltInNodeKinds.Literal)]
    [InlineData(BuiltInNodeKinds.Type)]
    public async Task CancellingTheTypeDialogCreatesNoNodeAndNoUndoEntry(string kind)
    {
        Editor.Dialogs.TypeAnswer = null;
        int nodes = Method.Nodes.Count;
        SuggestionItem item = await OpenAsync(Graph, Descriptor(kind).Suggestions[0].DisplayName);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        Assert.Equal(nodes, Method.Nodes.Count);
        Assert.False(ClassContext.UndoRedo.CanUndo);
        Assert.Equal(1, Editor.Dialogs.SelectTypeCalls.Count);
    }

    [Fact(Timeout = 60000)]
    public async Task CancellingTheMakeDelegateDialogCreatesNoNodeAndNoUndoEntry()
    {
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        ClassContext.UndoRedo.Clear();
        Editor.Dialogs.MethodAnswer = _ => null;
        SuggestionItem item = await OpenAsync(Graph, "Make Delegate For A Method Of String", upper.ReturnValuePins[0]);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        Assert.Empty(Method.Nodes.OfType<MakeDelegateNode>());
        Assert.False(ClassContext.UndoRedo.CanUndo);
        Assert.Equal(1, Editor.Dialogs.SelectMethodCalls);
    }

    [Fact(Timeout = 60000)]
    public async Task MakeDelegateAsksForAMethodAndCreatesItsNodeConnectedToThePin()
    {
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        Editor.Dialogs.MethodAnswer = methods => methods.First(method => method.Name == "ToUpperInvariant");
        SuggestionItem item = await OpenAsync(Graph, "Make Delegate For A Method Of String", upper.ReturnValuePins[0]);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        MakeDelegateNode node = Assert.Single(Method.Nodes.OfType<MakeDelegateNode>());
        Assert.Same(upper.ReturnValuePins[0], node.TargetPin.IncomingPin);
        Assert.Equal("Add node", ClassContext.UndoRedo.UndoName);
    }

    [Fact(Timeout = 60000)]
    public async Task AnExtensionSuggestionCreatesItsNodeAtThePositionAsOneUndoStep()
    {
        SuggestionItem log = await OpenAsync(Graph, "Log", position: new GraphPoint(40, 60));

        await Graph.Search.SelectCommand.ExecuteAsync(log);

        Node node = Method.Nodes.Single(n => n.GetType().Name == "LogNode");
        Assert.Equal((40, 60), (node.PositionX, node.PositionY));
        ClassContext.UndoRedo.Undo();
        Assert.DoesNotContain(node, Method.Nodes);
        ClassContext.UndoRedo.Redo();
        Assert.Contains(node, Method.Nodes);
    }

    [Fact(Timeout = 60000)]
    public async Task CustomEventsGetUniqueNames()
    {
        EventGraph events = ClassContext.CreateEventGraph();
        using var view = new NodeGraphViewModel(events, ClassContext.Services);

        await view.Search.SelectCommand.ExecuteAsync(await OpenAsync(view, CustomEventSuggestion.DisplayText));
        await view.Search.SelectCommand.ExecuteAsync(await OpenAsync(view, CustomEventSuggestion.DisplayText));

        string[] names = [.. events.Entries.Select(entry => entry.EventName)];
        Assert.Equal(2, names.Length);
        Assert.Equal(CustomEventSuggestion.NamePrefix, names[0]);
        Assert.NotEqual(names[0], names[1]);
        Assert.StartsWith(CustomEventSuggestion.NamePrefix, names[1], StringComparison.Ordinal);
    }

    [Fact(Timeout = 60000)]
    public async Task AnOverrideEntryTakesTheBaseMethodAndIsNotOfferedAgain()
    {
        EventGraph events = ClassContext.CreateEventGraph();
        using var view = new NodeGraphViewModel(events, ClassContext.Services);

        await view.Search.SelectCommand.ExecuteAsync(await OpenAsync(view, "Override ToString"));

        Assert.Equal("ToString", Assert.Single(events.Entries).OverriddenMethod?.Name);
        await view.OpenSearchAsync(new GraphPoint(0, 0), null, Token);
        Assert.DoesNotContain(view.Search.AllSuggestions, item => item.Text == "Override ToString");
    }

    [Fact(Timeout = 60000)]
    public async Task AVariableOpensTheChooserAndGetCreatesAGetterAtThePosition()
    {
        await Graph.OpenSearchAsync(new GraphPoint(42, 24), null, Token);
        SuggestionItem empty = Graph.Search.AllSuggestions.First(item => item.Value is VariableSpecifier { Name: "Empty" });

        await Graph.Search.SelectCommand.ExecuteAsync(empty);
        Graph.GetSetChooser.GetCommand.Execute(null);

        VariableGetterNode node = Assert.Single(Method.Nodes.OfType<VariableGetterNode>());
        Assert.Equal((42, 24), (node.PositionX, node.PositionY));
        Assert.Equal("Add node", ClassContext.UndoRedo.UndoName);
    }

    [Fact(Timeout = 60000)]
    public async Task AnExecOutputIsConnectedToTheNewNodesExecInput()
    {
        NodeOutputExecPin entry = Method.EntryNode.InitialExecutionPin;
        SuggestionItem ifElse = await OpenAsync(Graph, "If Else", entry);

        await Graph.Search.SelectCommand.ExecuteAsync(ifElse);

        Assert.Same(Method.Nodes.OfType<IfElseNode>().Single().InputExecPins[0], entry.OutgoingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task AnInputDataPinIsConnectedToTheNewNodesResult()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        SuggestionItem readLine = await OpenAsync(Graph, "System.Console ReadLine () : System.String", write.ArgumentPins[0]);

        await Graph.Search.SelectCommand.ExecuteAsync(readLine);

        CallMethodNode read = Method.Nodes.OfType<CallMethodNode>().Single(node => node.MethodName == "ReadLine");
        Assert.Same(read.ReturnValuePins[0], write.ArgumentPins[0].IncomingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task AnOutputDataPinIsConnectedToTheNewNodesArgument()
    {
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        SuggestionItem write = await OpenAsync(Graph, "System.Console WriteLine (value: System.String)", upper.ReturnValuePins[0]);

        await Graph.Search.SelectCommand.ExecuteAsync(write);

        CallMethodNode written = Method.Nodes.OfType<CallMethodNode>().Single(node => node.MethodName == "WriteLine");
        Assert.Same(upper.ReturnValuePins[0], written.ArgumentPins[0].IncomingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task ANegativePositionIsClampedToTheCanvas()
    {
        SuggestionItem ifElse = await OpenAsync(Graph, "If Else", position: new GraphPoint(-50, -20));

        await Graph.Search.SelectCommand.ExecuteAsync(ifElse);

        IfElseNode node = Method.Nodes.OfType<IfElseNode>().Single();
        Assert.Equal((0, 0), (node.PositionX, node.PositionY));
    }

    [Fact(Timeout = 60000)]
    public async Task UndoAndRedoRemoveAndRestoreTheCreatedNodeAndItsConnection()
    {
        NodeOutputExecPin entry = Method.EntryNode.InitialExecutionPin;
        NodeInputExecPin? previous = entry.OutgoingPin;
        await Graph.Search.SelectCommand.ExecuteAsync(await OpenAsync(Graph, "If Else", entry));
        IfElseNode node = Method.Nodes.OfType<IfElseNode>().Single();

        ClassContext.UndoRedo.Undo();
        Assert.DoesNotContain(node, Method.Nodes);
        Assert.Same(previous, entry.OutgoingPin);

        ClassContext.UndoRedo.Redo();
        Assert.Contains(node, Method.Nodes);
        Assert.Same(node.InputExecPins[0], entry.OutgoingPin);
    }
}
