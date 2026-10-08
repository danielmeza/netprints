using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Events;

/// <summary>US8: renaming an event graph with F2 and in the inspector, and the event entry inspector.</summary>
public class EventInspectorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task RenameInInspectorAsync(EditorSession session, NetPrints.Testing.Ui.Driving.UiElement box, string name)
    {
        await box.ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync(name, Token);
        await session.Driver.PressAsync("Tab", Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task F2OnAnEventGraphRowRenamesItInTheInspectorAndUpdatesTheTreeAndTheTab()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.RunAsync("addEventGraph", Token);
        EventGraph graph = Assert.Single(session.Class.EventGraphs);
        var tree = session.Page.Tree;
        var row = await tree.RevealAsync(tree.Item(AutomationIds.TreeKindEventGraph, graph.Name), ProjectTreePage.EventGraphsGroup, Token);
        await tree.SelectAsync(row, Token);

        await session.Driver.PressAsync("F2", Token);
        await session.Page.Inspector.EventGraphInspector.WaitVisibleAsync(Token);
        await RenameInInspectorAsync(session, session.Page.Inspector.EventGraphName, "Gameplay");

        Assert.Equal("Gameplay", graph.Name);
        await tree.Item(AutomationIds.TreeKindEventGraph, "Gameplay").WaitVisibleAsync(Token);
        DocumentId id = CommandTargets.GraphDocumentOf(session.App.Session, graph) ?? throw new InvalidOperationException("No document.");
        await session.Page.Tabs.Tab(id).WaitUntilAsync(e => (e.Name ?? "").StartsWith("Gameplay", StringComparison.Ordinal), "the tab renamed", Token);

        await session.PressUndoAsync(Token);

        Assert.Equal("EventGraph", graph.Name);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADuplicateEventGraphNameIsRefusedWithTheMessage()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.RunAsync("addEventGraph", Token);
        await session.RunAsync("addEventGraph", Token);
        EventGraph second = session.Class.EventGraphs[1];
        session.App.Shell.TreeSelection = second;
        await session.Page.Inspector.EventGraphInspector.WaitVisibleAsync(Token);

        await RenameInInspectorAsync(session, session.Page.Inspector.EventGraphName, session.Class.EventGraphs[0].Name);

        await session.Page.Inspector.EventGraphError.WaitUntilAsync(
            e => (e.Text ?? "") == $"An event graph named '{session.Class.EventGraphs[0].Name}' already exists", "the refusal shown", Token);
        Assert.NotEqual(session.Class.EventGraphs[0].Name, second.Name);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ASelectedCustomEntryShowsItsInspectorAndAddArgumentIsOneUndoStep()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.RunAsync("addEventGraph", Token);
        await session.Graph.Watermark.WaitUntilAsync(e => !string.IsNullOrEmpty(e.Text), "graph shown", Token);
        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
        await search.FilterAsync("Custom Event", "Custom Event", Token);
        await search.ChooseAsync("Custom Event", Token);
        await session.WaitForRenderedAsync(Token);
        var entry = Assert.Single(((EventGraph)session.GraphViewModel.Graph).Nodes.OfType<EventEntryNode>());

        foreach (NodeViewModel node in session.GraphViewModel.Nodes)
        {
            node.IsSelected = ReferenceEquals(node.Node, entry);
        }

        await session.Page.Inspector.EventEntryInspector.WaitVisibleAsync(Token);
        await session.Page.Inspector.EventEntryKind.WaitUntilAsync(e => e.Text == "Custom event", "kind shown", Token);
        await session.Page.Inspector.EventEntryAddArgument.ClickAsync(Token);
        await session.Page.Inspector.EventEntryArgumentName.WaitVisibleAsync(Token);

        Assert.Equal([new EventArgument("arg", TypeSpecifier.FromType<object>())], entry.Arguments);
        Assert.Equal("Add argument", session.ClassContext.UndoRedo.UndoName);

        await RenameInInspectorAsync(session, session.Page.Inspector.EventEntryName, "OnFrame");

        Assert.Equal("OnFrame", entry.EventName);
        Assert.Equal("Rename event", session.ClassContext.UndoRedo.UndoName);
    }
}
