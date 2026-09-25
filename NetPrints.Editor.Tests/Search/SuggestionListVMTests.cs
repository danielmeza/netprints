using System.Diagnostics;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Graph;

namespace NetPrints.Editor.Tests.Search;

public class SuggestionListVMTests : GraphTestBase
{
    public SuggestionListVMTests(TestEditor editor) : base(editor)
    {
        Graph.Search.FilterThrottle = TimeSpan.Zero;
    }

    private static List<string> Categories(IEnumerable<SuggestionItem> rows) =>
        rows.Where(r => r.IsHeader).Select(r => r.Category).ToList();

    [Fact]
    public void BuiltInNodesPerGraphKind()
    {
        List<string> BuiltIns(NodeGraphVM graph) =>
            graph.Search.BuildItems(null).Where(i => i.Category == "NetPrints" && !i.IsHeader).Select(i => i.Text).ToList();

        var methodBuiltIns = BuiltIns(Graph);
        Assert.Equal(new[]
        {
            "For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Return", "Make Array",
            "Literal", "Type", "Make Array Type", "Throw", "Await", "Ternary", "Default",
        }, methodBuiltIns);

        ClassEditor.CreateConstructorCommand.Execute(null);
        var ctorBuiltIns = BuiltIns(ClassEditor.OpenedGraph!);
        Assert.Equal(12, ctorBuiltIns.Count());
        Assert.DoesNotContain("Return", ctorBuiltIns);
        Assert.DoesNotContain("Await", ctorBuiltIns);

        ClassEditor.ShowClassCommand.Execute(null);
        Assert.Equal(new[] { "Type", "Make Array Type" }, BuiltIns(ClassEditor.OpenedGraph!));
    }

    [Fact]
    public void CategoriesPerPinKind()
    {
        var rowsNoPin = Graph.Search.BuildItems(null);
        Assert.Equal(new[] { "NetPrints", "This Methods", "Static Methods", "Static Variables" }, Categories(rowsNoPin).ToArray());

        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        var stringIn = Categories(Graph.Search.BuildItems(write.ArgumentPins[0]));
        Assert.Equal(new[] { "Static Methods" }, stringIn.ToArray());

        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        var stringOut = upper.OutputDataPins.First(p => p.Name != "Exception");
        var stringOutCategories = Categories(Graph.Search.BuildItems(stringOut));
        Assert.Equal(new[] { "NetPrints", "Pin Variables", "Pin Methods", "Static Methods" }, stringOutCategories.ToArray());

        Assert.Equal(new[] { "NetPrints", "This Methods", "Static Methods" }, Categories(Graph.Search.BuildItems(write.InputExecPins[0])).ToArray());

        var makeArrayType = new MakeArrayTypeNode(Method);
        Assert.Equal(new[] { "Types" }, Categories(Graph.Search.BuildItems(makeArrayType.InputTypePins[0])).ToArray());

        var intType = new TypeNode(Method, IntType);
        var typeOut = Categories(Graph.Search.BuildItems(intType.OutputTypePins[0]));
        Assert.Equal(new[] { "Pin Static Methods", "Generic Types", "Generic Static Methods" }, typeOut.ToArray());
    }

    [Fact(Timeout = 60000)]
    public async Task MultiTermCaseInsensitiveFilterWithHeaders()
    {
        await Graph.OpenSearchAsync(new GraphPoint(10, 10), null, TestContext.Current.CancellationToken);
        var search = Graph.Search;
        Assert.True(search.IsOpen);
        Assert.False(search.IsLoading);
        int all = search.Items.Count;

        search.SearchText = "WRITE line console";

        Assert.True(search.Items.Count < all);
        Assert.True(search.Items.Where(i => !i.IsHeader).All(i =>
            i.SearchText.Contains("write", StringComparison.OrdinalIgnoreCase)
            && i.SearchText.Contains("console", StringComparison.OrdinalIgnoreCase)));
        Assert.True(search.Items.Any(i => i.Value is MethodSpecifier { Name: "WriteLine" }));
        Assert.True(search.Items[0].IsHeader, "rows are grouped under category headers");
        Assert.True(search.Items.Where(i => i.IsHeader).All(h => search.Items.Any(i => !i.IsHeader && i.Category == h.Category)), "empty categories are hidden");

        search.SearchText = "";
        Assert.Equal(all, search.Items.Count);
    }

    [Fact(Timeout = 60000)]
    public async Task SelectingMethodCreatesNodeAtPositionAndCloses()
    {
        await Graph.OpenSearchAsync(new GraphPoint(140, 84), null, TestContext.Current.CancellationToken);
        Graph.Search.SearchText = "Console WriteLine";
        var item = Graph.Search.Items.First(i => i.Value is MethodSpecifier { Name: "WriteLine" } m && m.Parameters.Count == 1);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        var node = Method.Nodes.OfType<CallMethodNode>().Single();
        Assert.Equal(140, node.PositionX);
        Assert.Equal(84, node.PositionY);
        Assert.False(Graph.Search.IsOpen);
    }

    [Fact(Timeout = 60000)]
    public async Task PinSearchConnectsNewNode()
    {
        var entryExec = Method.EntryNode.InitialExecutionPin;
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), entryExec, TestContext.Current.CancellationToken);
        Assert.Null(entryExec.OutgoingPin);

        var ifElse = Graph.Search.Items.First(i => i.Text == "If Else");
        await Graph.Search.SelectCommand.ExecuteAsync(ifElse);

        var node = Method.Nodes.OfType<IfElseNode>().Single();
        Assert.Same(node.InputExecPins[0], entryExec.OutgoingPin);
    }

    [Fact(Timeout = 60000)]
    public async Task SpecialItemsAskForTypeOrMethod()
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        SuggestionItem Item(string text) => Graph.Search.AllSuggestions.First(i => i.Text == text);

        Editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<List<int>>();
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Construct New Object"));
        var constructed = Method.Nodes.OfType<ConstructorNode>().Single();
        Assert.Equal(TypeSpecifier.FromType<List<int>>(), constructed.ConstructorSpecifier.DeclaringType);
        Assert.Equal(TypeSpecifier.FromType<object>(), Editor.Dialogs.SelectTypeCalls[0]);

        Editor.Dialogs.TypeAnswer = IntType;
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Literal"));
        Assert.Equal(IntType, Method.Nodes.OfType<LiteralNode>().Single().LiteralType);

        await Graph.Search.SelectCommand.ExecuteAsync(Item("Type"));
        Assert.Equal(1, Method.Nodes.OfType<TypeNode>().ToList().Count());

        Editor.Dialogs.TypeAnswer = null;
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Literal"));
        Assert.Equal(1, Method.Nodes.OfType<LiteralNode>().ToList().Count());
        Assert.Equal(4, Editor.Dialogs.SelectTypeCalls.Count());

        // Make Delegate asks for a method.
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), upper.OutputDataPins.First(p => p.Name != "Exception"), TestContext.Current.CancellationToken);
        var makeDelegate = Graph.Search.AllSuggestions.First(i => i.Value is MakeDelegateTypeInfo);
        Assert.StartsWith("Make Delegate For A Method Of", makeDelegate.Text);
        await Graph.Search.SelectCommand.ExecuteAsync(makeDelegate);
        Assert.Equal(1, Editor.Dialogs.SelectMethodCalls);
        Assert.NotEmpty(Editor.Dialogs.LastMethods);
        Assert.Equal(1, Method.Nodes.OfType<MakeDelegateNode>().ToList().Count());
    }

    [Fact(Timeout = 60000)]
    public async Task VariableOpensGetSetChooser()
    {
        await Graph.OpenSearchAsync(new GraphPoint(42, 42), null, TestContext.Current.CancellationToken);
        var property = Graph.Search.AllSuggestions.First(i => i.Value is VariableSpecifier);
        Assert.Equal("Property_16x.png", property.IconKey);

        await Graph.Search.SelectCommand.ExecuteAsync(property);

        Assert.True(Graph.GetSetChooser.IsOpen);
        Assert.Equal(new GraphPoint(42, 42), Graph.GetSetChooser.Position);
    }

    [Fact]
    public void IconsAndTextsFollowWpfConverter()
    {
        var rows = Graph.Search.BuildItems(null);
        Assert.Equal("If_16x.png", rows.First(r => r.Text == "If Else").IconKey);
        Assert.Equal("Loop_16x.png", rows.First(r => r.Text == "For Loop").IconKey);
        var op = rows.First(r => r.Value is MethodSpecifier m && m.Name == "op_Addition");
        Assert.Equal("Operator_16x.png", op.IconKey);
        Assert.Contains("Operator", op.Text);
        Assert.Equal("Method_16x.png", rows.First(r => r.Value is MethodSpecifier { Name: "WriteLine" }).IconKey);
    }

    [Fact(Timeout = 60000)]
    public async Task ThrottledFilterAppliesAfterDelay()
    {
        var search = Graph.Search;
        search.FilterThrottle = TimeSpan.FromMilliseconds(100);
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        int all = search.Items.Count;

        search.SearchText = "c";
        search.SearchText = "co";
        search.SearchText = "console writeline";
        Assert.Equal(all, search.Items.Count);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (search.Items.Count == all && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(search.Items.Count < all);
    }
}
