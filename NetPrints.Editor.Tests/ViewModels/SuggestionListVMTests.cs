using System.Diagnostics;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class SuggestionListVMTests : GraphTestBase
{
    [TestInitialize]
    public void Immediate() => Graph.Search.FilterThrottle = TimeSpan.Zero;

    private static List<string> Categories(IEnumerable<SuggestionItem> rows) =>
        rows.Where(r => r.IsHeader).Select(r => r.Category).ToList();

    [TestMethod]
    public void BuiltInNodesPerGraphKind()
    {
        List<string> BuiltIns(NodeGraphVM graph) =>
            graph.Search.BuildItems(null).Where(i => i.Category == "NetPrints" && !i.IsHeader).Select(i => i.Text).ToList();

        var methodBuiltIns = BuiltIns(Graph);
        CollectionAssert.AreEqual(new[]
        {
            "For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Return", "Make Array",
            "Literal", "Type", "Make Array Type", "Throw", "Await", "Ternary", "Default",
        }, methodBuiltIns);

        ClassEditor.CreateConstructorCommand.Execute(null);
        var ctorBuiltIns = BuiltIns(ClassEditor.OpenedGraph!);
        Assert.HasCount(12, ctorBuiltIns);
        CollectionAssert.DoesNotContain(ctorBuiltIns, "Return");
        CollectionAssert.DoesNotContain(ctorBuiltIns, "Await");

        ClassEditor.ShowClassCommand.Execute(null);
        CollectionAssert.AreEqual(new[] { "Type", "Make Array Type" }, BuiltIns(ClassEditor.OpenedGraph!));
    }

    [TestMethod]
    public void CategoriesPerPinKind()
    {
        var rowsNoPin = Graph.Search.BuildItems(null);
        CollectionAssert.AreEqual(new[] { "NetPrints", "This Methods", "Static Methods", "Static Variables" },
            Categories(rowsNoPin).ToArray(), string.Join(",", Categories(rowsNoPin)));

        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        var stringIn = Categories(Graph.Search.BuildItems(write.ArgumentPins[0]));
        CollectionAssert.AreEqual(new[] { "Static Methods" }, stringIn.ToArray(), string.Join(",", stringIn));

        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        var stringOut = upper.OutputDataPins.First(p => p.Name != "Exception");
        var stringOutCategories = Categories(Graph.Search.BuildItems(stringOut));
        CollectionAssert.AreEqual(new[] { "NetPrints", "Pin Variables", "Pin Methods", "Static Methods" },
            stringOutCategories.ToArray(), string.Join(",", stringOutCategories));

        CollectionAssert.AreEqual(new[] { "NetPrints", "This Methods", "Static Methods" },
            Categories(Graph.Search.BuildItems(write.InputExecPins[0])).ToArray());

        var makeArrayType = new MakeArrayTypeNode(Method);
        CollectionAssert.AreEqual(new[] { "Types" }, Categories(Graph.Search.BuildItems(makeArrayType.InputTypePins[0])).ToArray());

        var intType = new TypeNode(Method, IntType);
        var typeOut = Categories(Graph.Search.BuildItems(intType.OutputTypePins[0]));
        CollectionAssert.AreEqual(new[] { "Pin Static Methods", "Generic Types", "Generic Static Methods" }, typeOut.ToArray(), string.Join(",", typeOut));
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task MultiTermCaseInsensitiveFilterWithHeaders()
    {
        await Graph.OpenSearchAsync(new GraphPoint(10, 10));
        var search = Graph.Search;
        Assert.IsTrue(search.IsOpen);
        Assert.IsFalse(search.IsLoading);
        int all = search.Items.Count;

        search.SearchText = "WRITE line console";

        Assert.IsLessThan(all, search.Items.Count);
        Assert.IsTrue(search.Items.Where(i => !i.IsHeader).All(i =>
            i.SearchText.Contains("write", StringComparison.OrdinalIgnoreCase)
            && i.SearchText.Contains("console", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(search.Items.Any(i => i.Value is MethodSpecifier { Name: "WriteLine" }));
        Assert.IsTrue(search.Items[0].IsHeader, "rows are grouped under category headers");
        Assert.IsTrue(search.Items.Where(i => i.IsHeader).All(h => search.Items.Any(i => !i.IsHeader && i.Category == h.Category)),
            "empty categories are hidden");

        search.SearchText = "";
        Assert.AreEqual(all, search.Items.Count);
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task SelectingMethodCreatesNodeAtPositionAndCloses()
    {
        await Graph.OpenSearchAsync(new GraphPoint(140, 84));
        Graph.Search.SearchText = "Console WriteLine";
        var item = Graph.Search.Items.First(i => i.Value is MethodSpecifier { Name: "WriteLine" } m && m.Parameters.Count == 1);

        await Graph.Search.SelectCommand.ExecuteAsync(item);

        var node = Method.Nodes.OfType<CallMethodNode>().Single();
        Assert.AreEqual(140, node.PositionX);
        Assert.AreEqual(84, node.PositionY);
        Assert.IsFalse(Graph.Search.IsOpen);
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task PinSearchConnectsNewNode()
    {
        var entryExec = Method.EntryNode.InitialExecutionPin;
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), entryExec);
        Assert.IsNull(entryExec.OutgoingPin, "the dragged exec output is disconnected first (as in WPF)");

        var ifElse = Graph.Search.Items.First(i => i.Text == "If Else");
        await Graph.Search.SelectCommand.ExecuteAsync(ifElse);

        var node = Method.Nodes.OfType<IfElseNode>().Single();
        Assert.AreSame(node.InputExecPins[0], entryExec.OutgoingPin);
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task SpecialItemsAskForTypeOrMethod()
    {
        await Graph.OpenSearchAsync(new GraphPoint(0, 0));
        SuggestionItem Item(string text) => Graph.Search.AllSuggestions.First(i => i.Text == text);

        Editor.Dialogs.TypeAnswer = TypeSpecifier.FromType<List<int>>();
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Construct New Object"));
        var constructed = Method.Nodes.OfType<ConstructorNode>().Single();
        Assert.AreEqual(TypeSpecifier.FromType<List<int>>(), constructed.ConstructorSpecifier.DeclaringType);
        Assert.AreEqual(TypeSpecifier.FromType<object>(), Editor.Dialogs.SelectTypeCalls[0], "the type dialog defaults to object");

        Editor.Dialogs.TypeAnswer = IntType;
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Literal"));
        Assert.AreEqual(IntType, Method.Nodes.OfType<LiteralNode>().Single().LiteralType);

        await Graph.Search.SelectCommand.ExecuteAsync(Item("Type"));
        Assert.HasCount(1, Method.Nodes.OfType<TypeNode>().ToList());

        Editor.Dialogs.TypeAnswer = null;
        await Graph.Search.SelectCommand.ExecuteAsync(Item("Literal"));
        Assert.HasCount(1, Method.Nodes.OfType<LiteralNode>().ToList(), "cancelling the dialog creates nothing");
        Assert.HasCount(4, Editor.Dialogs.SelectTypeCalls);

        // Make Delegate asks for a method.
        var upper = new CallMethodNode(Method, FindMethod(typeof(string), "ToUpperInvariant"));
        await Graph.OpenSearchAsync(new GraphPoint(0, 0), upper.OutputDataPins.First(p => p.Name != "Exception"));
        var makeDelegate = Graph.Search.AllSuggestions.First(i => i.Value is MakeDelegateTypeInfo);
        StringAssert.StartsWith(makeDelegate.Text, "Make Delegate For A Method Of");
        await Graph.Search.SelectCommand.ExecuteAsync(makeDelegate);
        Assert.AreEqual(1, Editor.Dialogs.SelectMethodCalls);
        Assert.IsNotEmpty(Editor.Dialogs.LastMethods);
        Assert.HasCount(1, Method.Nodes.OfType<MakeDelegateNode>().ToList());
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task VariableOpensGetSetChooser()
    {
        await Graph.OpenSearchAsync(new GraphPoint(42, 42));
        var property = Graph.Search.AllSuggestions.First(i => i.Value is VariableSpecifier);
        Assert.AreEqual("Property_16x.png", property.IconKey);

        await Graph.Search.SelectCommand.ExecuteAsync(property);

        Assert.IsTrue(Graph.GetSetChooser.IsOpen);
        Assert.AreEqual(new GraphPoint(42, 42), Graph.GetSetChooser.Position);
    }

    [TestMethod]
    public void IconsAndTextsFollowWpfConverter()
    {
        var rows = Graph.Search.BuildItems(null);
        Assert.AreEqual("If_16x.png", rows.First(r => r.Text == "If Else").IconKey);
        Assert.AreEqual("Loop_16x.png", rows.First(r => r.Text == "For Loop").IconKey);
        var op = rows.First(r => r.Value is MethodSpecifier m && m.Name == "op_Addition");
        Assert.AreEqual("Operator_16x.png", op.IconKey);
        StringAssert.Contains(op.Text, "Operator");
        Assert.AreEqual("Method_16x.png", rows.First(r => r.Value is MethodSpecifier { Name: "WriteLine" }).IconKey);
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(120000, CooperativeCancellation = true)]
    public void BuildAndFilterPerformance()
    {
        // SC-005: < 2 s to build, < 300 ms per keystroke (3x budget on shared CI runners).
        double budget = Environment.GetEnvironmentVariable("CI") == "true" ? 3 : 1;
        var search = Graph.Search;

        // Warm the reflection caches used by every search (the editor does this when a project opens).
        search.BuildItems(null);

        var sw = Stopwatch.StartNew();
        var rows = search.BuildItems(null);
        search.SetItems(rows);
        double buildMs = sw.Elapsed.TotalMilliseconds;

        // Public static members of the full runtime set visible from the class (~42k rows on .NET 10).
        Assert.IsGreaterThan(30_000, rows.Count, "the full runtime set is searched");

        var keystrokes = new[] { "w", "wr", "wri", "writ", "write", "write ", "write l", "write li", "write lin", "write line" };
        double worstMs = 0;
        foreach (var text in keystrokes)
        {
            sw.Restart();
            search.SearchText = text;
            worstMs = Math.Max(worstMs, sw.Elapsed.TotalMilliseconds);
        }

        Console.WriteLine($"Suggestions: {rows.Count} rows, build+bind {buildMs:F0} ms, worst keystroke {worstMs:F0} ms");
        Assert.IsLessThan(2000 * budget, buildMs, $"build took {buildMs:F0} ms");
        Assert.IsLessThan(300 * budget, worstMs, $"filter took {worstMs:F0} ms");
        Assert.IsTrue(search.Items.Any(i => i.Value is MethodSpecifier { Name: "WriteLine" }));
    }

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public async Task ThrottledFilterAppliesAfterDelay()
    {
        var search = Graph.Search;
        search.FilterThrottle = TimeSpan.FromMilliseconds(100);
        await Graph.OpenSearchAsync(new GraphPoint(0, 0));
        await Task.Delay(300);
        int all = search.Items.Count;

        search.SearchText = "c";
        search.SearchText = "co";
        search.SearchText = "console writeline";
        Assert.AreEqual(all, search.Items.Count, "not filtered before the debounce time");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (search.Items.Count == all && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.IsLessThan(all, search.Items.Count);
    }
}
