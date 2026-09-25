using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Search;

public class NodeSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickOpensSearchAndTypingFilters()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);

        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token); // PAR-52
        Assert.Null(session.GraphVM.Search.SuggestionPin);
        await search.SearchBox.WaitUntilAsync(e => e["IsFocused"] == "True", "search box focused", Token);
        Assert.Equal("", await search.SearchBox.TextAsync(Token) ?? "");
        Assert.Equal(700, await search.View.GetAsync<double>("Width", Token));
        Assert.Equal(300, await search.View.GetAsync<double>("Height", Token));

        await search.FilterAsync("write line", r => r.Contains(" WriteLine ", StringComparison.Ordinal), Token);
        Assert.All(session.GraphVM.Search.Items.Where(i => !i.IsHeader), i => Assert.Contains("line", i.SearchText, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(session.GraphVM.Search.Items, i => i.Value is MethodSpecifier { Name: "WriteLine" });
        Assert.True((await search.RowTextsAsync(Token)).Count < 200, "the result list is virtualized");

        await search.PressEnterAsync(Token);
        await search.WaitClosedAsync(Token);
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(4, await session.Graph.NodeCountAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReleasingPinDragOnCanvasOpensFilteredSearch()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode);
        var execOut = write.OutputExecPins.First(p => p.Pin.Name != "Catch");

        await session.Graph.Node("CallMethodNode").Output(execOut.Pin.Name).DragCableToAsync(await session.Graph.EmptyPointAsync(Token), Token); // PAR-47
        var search = await session.Graph.Search.WaitOpenAsync(Token);

        Assert.Same(execOut.Pin, session.GraphVM.Search.SuggestionPin);
        await search.FilterAsync("If Else", "If Else", Token);
        await search.ChooseAsync("If Else", Token);
        await session.WaitForRenderedAsync(Token);

        var ifElse = ((MethodGraph)session.GraphVM.Graph).Nodes.OfType<IfElseNode>().Single();
        Assert.Same(ifElse.InputExecPins[0], ((CallMethodNode)write.Node).OutputExecPins[0].OutgoingPin);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task LiteralAsksForType()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        session.App.Dialogs.TypeAnswer = TypeSpecifier.FromType<int>();

        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
        await search.FilterAsync("literal", "Literal", Token);
        var icon = await search.RowIcon("Literal").GetAsync(Token); // 16-px icons
        Assert.Equal("True", icon["HasSource"]);
        Assert.Equal(16, icon.Bounds.Width);
        await search.ChooseAsync("Literal", Token);
        await session.WaitForRenderedAsync(Token);

        var method = (MethodGraph)session.GraphVM.Graph;
        Assert.Single(session.App.Dialogs.SelectTypeCalls); // PAR-54, 58
        Assert.Equal(TypeSpecifier.FromType<int>(), method.Nodes.OfType<LiteralNode>().Single().LiteralType);
    }
}
