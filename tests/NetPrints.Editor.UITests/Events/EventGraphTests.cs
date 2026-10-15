using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Search;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Events;

/// <summary>US4, ED-T07: event graphs (create, open, add a custom event via search, remove undoable).</summary>
public class EventGraphTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task OpenFromTheTreeAsync(EditorSession session, string name)
    {
        var tree = session.Page.Tree;
        var row = await tree.RevealAsync(tree.Item(AutomationIds.TreeKindEventGraph, name), ProjectTreePage.EventGraphsGroup, Token);
        await tree.OpenAsync(row, async () => (await session.Graph.Watermark.TryGetAsync(Token))?.Text == name, $"graph '{name}' shown", Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CreateOpenAddCustomEventViaSearchAndRemoveUndoable()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        // Create (US4): a new event graph named uniquely, added to the class; Create also opens it,
        // the same way Create Method/Create Constructor do. It starts empty (data-model.md §4), so
        // this waits on the watermark alone: GraphCanvas.WaitForGraphAsync also waits for nodes/cables
        // to "render", which never becomes true for a graph with zero of both.
        await session.RunAsync("addEventGraph", Token);
        string name = Assert.Single(session.Class.EventGraphs).Name;
        Assert.Equal("EventGraph", name);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);

        // Open: switch away, then reopen by double click on the tree row.
        await session.Page.OpenMethodAsync("Main", Token);
        await OpenFromTheTreeAsync(session, name);
        Assert.Same(session.Class.EventGraphs.Single(g => g.Name == name), session.GraphViewModel.Graph);

        // Add a custom event via search: right click empty canvas, choose "Custom Event" (US4).
        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
        await search.FilterAsync("Custom Event", "Custom Event", Token);
        Assert.Contains(session.GraphViewModel.Search.Items, i => i.Text == "Custom Event" && i.Value is CustomEventSuggestion);
        await search.ChooseAsync("Custom Event", Token);
        await session.WaitForRenderedAsync(Token);

        var eventGraph = (EventGraph)session.GraphViewModel.Graph;
        Assert.Empty(session.App.Dialogs.Errors);
        var entry = Assert.Single(eventGraph.Nodes.OfType<EventEntryNode>());
        Assert.Equal("CustomEvent", entry.EventName); // unique against the class's methods and entries
        Assert.Equal(["EventEntryNode"], await session.Graph.NodeNamesAsync(Token));

        // Also add an override entry via search ("Override <method>", US4): ToString is overridable on this class.
        var overrideSearch = await (await session.Graph.RightClickAtAsync(400, 400, Token)).WaitOpenAsync(Token);
        await overrideSearch.FilterAsync("Override ToString", "Override ToString", Token);
        await overrideSearch.ChooseAsync("Override ToString", Token);
        await session.WaitForRenderedAsync(Token);

        var overrideEntry = eventGraph.Nodes.OfType<EventEntryNode>().Single(e => e.EventName == "ToString");
        Assert.Equal(MethodModifiers.Override, overrideEntry.Modifiers);
        Assert.NotNull(overrideEntry.OverriddenMethod);
        Assert.Equal(2, eventGraph.Nodes.OfType<EventEntryNode>().Count());

        // Remove (undoable): through the project actions, as the tree's Delete does; it closes the graph's tab.
        DocumentId document = CommandTargets.GraphDocumentOf(session.App.Session, eventGraph) ?? throw new InvalidOperationException("The event graph has no document.");
        await (session.App.Composition.ProjectActions?.DeleteItemAsync(eventGraph, Token) ?? Task.CompletedTask);
        Assert.DoesNotContain(session.Class.EventGraphs, g => g.Name == name);
        Assert.DoesNotContain(document, session.App.Api.OpenDocuments);

        await session.PressUndoAsync(Token);
        Assert.Same(eventGraph, session.Class.EventGraphs.Single(g => g.Name == name)); // undo restores the same instance, not a rebuilt one

        await session.PressRedoAsync(Token);
        Assert.DoesNotContain(session.Class.EventGraphs, g => g.Name == name);
    }

    /// <summary>OWN-07, owner's sequence: add an event graph, open it, then open Main, and back; each open shows its own graph.</summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningMainAfterAnEventGraphAndBackShowsEachGraph()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.RunAsync("addEventGraph", Token); // also opens it
        string name = Assert.Single(session.Class.EventGraphs).Name;
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);

        await session.Page.OpenMethodAsync("Main", Token);
        Assert.Same(session.ClassContext.Methods.Single(m => m.Name == "Main").Graph, session.GraphViewModel.Graph);

        await OpenFromTheTreeAsync(session, name);
        Assert.Same(session.ClassContext.EventGraphs.Single(g => g.Name == name).Graph, session.GraphViewModel.Graph);
    }
}
