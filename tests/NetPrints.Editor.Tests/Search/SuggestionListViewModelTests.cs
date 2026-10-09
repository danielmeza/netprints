using System.Diagnostics;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Search;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Search;

public class SuggestionListViewModelTests : GraphTestBase
{
    public SuggestionListViewModelTests(TestEditor editor) : base(editor)
    {
    }

    /// <summary>Types into the search box and lets the throttle window pass in virtual time.</summary>
    private void Type(SuggestionListViewModel search, string text)
    {
        search.SearchText = text;
        Editor.Scheduler.AdvanceBy(search.FilterThrottle.Ticks);
    }

    private static List<string> Categories(IEnumerable<SuggestionItem> rows) =>
        rows.Where(r => r.IsHeader).Select(r => r.Category).ToList();

    [Fact]
    public void BuiltInNodesPerGraphKind()
    {
        List<string> BuiltIns(NodeGraphViewModel graph) =>
            graph.Search.BuildItems(null).Where(i => i.Category == "NetPrints" && !i.IsHeader).Select(i => i.Text).ToList();

        var methodBuiltIns = BuiltIns(Graph);
        Assert.Equal(new[]
        {
            "For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Return", "Make Array",
            "Literal", "Type", "Make Array Type", "Throw", "Await", "Ternary", "Default",
        }, methodBuiltIns);

        using var ctorGraph = new NodeGraphViewModel(ClassContext.CreateConstructor(), ClassContext.Services);
        var ctorBuiltIns = BuiltIns(ctorGraph);
        Assert.Equal(12, ctorBuiltIns.Count());
        Assert.DoesNotContain("Return", ctorBuiltIns);
        Assert.DoesNotContain("Await", ctorBuiltIns);

        using var classGraph = new NodeGraphViewModel(Class, ClassContext.Services);
        Assert.Equal(new[] { "Type", "Make Array Type" }, BuiltIns(classGraph));
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

        Type(search, "WRITE line console");

        Assert.True(search.Items.Count < all);
        Assert.True(search.Items.Where(i => !i.IsHeader).All(i =>
            i.SearchText.Contains("write", StringComparison.OrdinalIgnoreCase)
            && i.SearchText.Contains("console", StringComparison.OrdinalIgnoreCase)));
        Assert.True(search.Items.Any(i => i.Value is MethodSpecifier { Name: "WriteLine" }));
        Assert.True(search.Items[0].IsHeader, "rows are grouped under category headers");
        Assert.True(search.Items.Where(i => i.IsHeader).All(h => search.Items.Any(i => !i.IsHeader && i.Category == h.Category)), "empty categories are hidden");

        Type(search, "");
        Assert.Equal(all, search.Items.Count);
    }

    [Fact(Timeout = 60000)]
    public async Task SelectingMethodCreatesNodeAtPositionAndCloses()
    {
        await Graph.OpenSearchAsync(new GraphPoint(140, 84), null, TestContext.Current.CancellationToken);
        Type(Graph.Search, "Console WriteLine");
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
        Assert.Equal(IconIds.CategoryProperty, property.IconKey);

        await Graph.Search.SelectCommand.ExecuteAsync(property);

        Assert.True(Graph.GetSetChooser.IsOpen);
        Assert.Equal(new GraphPoint(42, 42), Graph.GetSetChooser.Position);
    }

    [Fact]
    public void IconsAndTextsFollowWpfConverter()
    {
        var rows = Graph.Search.BuildItems(null);
        Assert.Equal(IconIds.CategoryIf, rows.First(r => r.Text == "If Else").IconKey);
        Assert.Equal(IconIds.CategoryLoop, rows.First(r => r.Text == "For Loop").IconKey);
        var op = rows.First(r => r.Value is MethodSpecifier m && m.Name == "op_Addition");
        Assert.Equal(IconIds.CategoryOperator, op.IconKey);
        Assert.Contains("Operator", op.Text);
        Assert.Equal(IconIds.CategoryMethod, rows.First(r => r.Value is MethodSpecifier { Name: "WriteLine" }).IconKey);
    }

    [Fact]
    public async Task IsFilteringWhileTheDebouncedFilterIsInFlight()
    {
        // FLAKE-01: a property that already matches the previous, unfiltered view (eg. "Major" on a
        // freshly-dropped Version pin) must not be clickable until the debounced filter actually
        // lands, or the row can be yanked out from under a click that landed just before it settled.
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        var search = Graph.Search;
        Assert.False(search.IsFiltering);

        search.SearchText = "major";
        Assert.True(search.IsFiltering);

        Editor.Scheduler.AdvanceBy(search.FilterThrottle.Ticks - 1);
        Assert.True(search.IsFiltering);

        Editor.Scheduler.AdvanceBy(1);
        Assert.False(search.IsFiltering);
    }

    [Fact(Timeout = 60000)]
    public async Task SelectFirstFlushesThePendingFilterBeforePickingTheFirstItem()
    {
        // R2-14: no AdvanceBy after setting SearchText, so the 100 ms throttle has not landed and
        // Items still shows the unfiltered list; SelectFirst must still pick "If Else", not whatever
        // the stale Items happened to have first.
        await Graph.OpenSearchAsync(new GraphPoint(10, 20), null, TestContext.Current.CancellationToken);
        var search = Graph.Search;
        Assert.NotEqual("If Else", search.Items.FirstOrDefault(i => !i.IsHeader)?.Text);

        search.SearchText = "If Else";
        Assert.True(search.IsFiltering);

        search.SelectFirstCommand.Execute(null);

        var node = Method.Nodes.OfType<IfElseNode>().Single();
        Assert.Equal(10, node.PositionX);
        Assert.Equal(20, node.PositionY);
    }

    [Fact(Timeout = 60000)]
    public async Task HighlightFirstSetsTheSelectedItemToTheFirstNonHeaderRow()
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        var search = Graph.Search;
        Type(search, "If Else");

        search.HighlightFirstCommand.Execute(null);

        Assert.Equal("If Else", search.SelectedItem?.Text);
        Assert.Empty(Method.Nodes.OfType<IfElseNode>()); // highlighting alone does not create a node
    }

    [Fact]
    public async Task TypingIsThrottledInVirtualTime()
    {
        var search = Graph.Search;
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), null, TestContext.Current.CancellationToken);
        int all = search.Items.Count;

        search.SearchText = "c";
        Editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        search.SearchText = "co";
        Editor.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        search.SearchText = "console writeline";
        Assert.Equal(all, search.Items.Count); // each keystroke restarts the throttle window

        Editor.Scheduler.AdvanceBy(search.FilterThrottle.Ticks - 1);
        Assert.Equal(all, search.Items.Count);

        Editor.Scheduler.AdvanceBy(1);
        Assert.True(search.Items.Count < all);
        Assert.Contains(search.Items, i => i.Value is MethodSpecifier { Name: "WriteLine" });
    }
}
