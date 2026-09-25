using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Search;

public class NodeSearchTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickOpensSearchAndTypingFilters()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;

        graph.RightClickEmpty(); // PAR-52
        var search = graph.Search;
        await search.WaitReadyAsync();
        Assert.Null(search.ViewModel.SuggestionPin);
        await HeadlessInput.WaitUntilAsync(() => search.SearchBox.IsFocused, "search box focused");
        Assert.Equal("", search.SearchBox.Text ?? "");
        Assert.Equal(700, search.View.Width);
        Assert.Equal(300, search.View.Height);

        search.Type("write line");
        await search.WaitFilteredAsync(i => i.SearchText.Contains("line", StringComparison.OrdinalIgnoreCase), "filtered to 'line'");
        Assert.Contains(search.ViewModel.Items, i => i.Value is MethodSpecifier { Name: "WriteLine" });
        Assert.True(search.RealizedRowCount < 200, "the result list is virtualized");

        search.PressEnter();
        await search.WaitClosedAsync();
        await graph.WaitForRenderedAsync();
        Assert.Equal(4, graph.ViewModel.Nodes.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReleasingPinDragOnCanvasOpensFilteredSearch()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        var write = graph.ViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var execOut = write.OutputExecPins.First(p => p.Pin.Name != "Catch");

        graph.DragPin(execOut, graph.EmptyPoint()); // PAR-47
        await graph.Search.WaitReadyAsync();

        Assert.Same(execOut.Pin, graph.Search.ViewModel.SuggestionPin);
        graph.Search.Type("If Else");
        await graph.Search.WaitFilteredAsync(i => i.SearchText.Contains("if", StringComparison.OrdinalIgnoreCase)
            && i.SearchText.Contains("else", StringComparison.OrdinalIgnoreCase), "filtered to 'If Else'");
        graph.Search.ClickRow("If Else");
        await graph.WaitForRenderedAsync();

        var ifElse = ((MethodGraph)graph.ViewModel.Graph).Nodes.OfType<IfElseNode>().Single();
        Assert.Same(ifElse.InputExecPins[0], ((CallMethodNode)write.Node).OutputExecPins[0].OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task LiteralAsksForType()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;
        session.Main.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();

        graph.RightClickEmpty();
        await graph.Search.WaitReadyAsync();
        graph.Search.Type("literal");
        await graph.Search.WaitFilteredAsync(i => i.SearchText.Contains("literal", StringComparison.OrdinalIgnoreCase), "filtered to 'literal'");

        Assert.NotNull(graph.Search.RowIcon("Literal").Source); // 16-px icons
        graph.Search.ClickRow("Literal");
        await HeadlessInput.WaitUntilAsync(() => ((MethodGraph)graph.ViewModel.Graph).Nodes.OfType<LiteralNode>().Any(), "literal created");

        Assert.Single(session.Main.Dialogs.SelectTypeCalls); // PAR-54, 58
        Assert.Equal(TypeSpecifier.FromType<int>(), ((MethodGraph)graph.ViewModel.Graph).Nodes.OfType<LiteralNode>().Single().LiteralType);
    }
}
