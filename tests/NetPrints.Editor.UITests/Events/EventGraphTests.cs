using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Search;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Events;

/// <summary>US4, ED-T07: event graphs (create, open, add a custom event via search, remove undoable).</summary>
public class EventGraphTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CreateOpenAddCustomEventViaSearchAndRemoveUndoable()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var events = session.ClassEditor.EventGraphs;

        // Create (US4): a new event graph named uniquely, added to the class; Create also opens it,
        // the same way Create Method/Create Constructor do. It starts empty (data-model.md §4), so
        // this waits on the watermark alone: GraphCanvas.WaitForGraphAsync also waits for nodes/cables
        // to "render", which never becomes true for a graph with zero of both (P3b guard/known gap:
        // WaitRenderedAsync's "#"-joined snapshot treats an empty join as still-loading).
        string name = await events.CreateAsync(Token);
        Assert.Equal("EventGraph", name);
        Assert.Contains(session.ClassVM.Class.EventGraphs, g => g.Name == name);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);

        // Open: switch away, then reopen by double click on the list row.
        await session.ClassEditor.OpenMethodAsync("Main", Token);
        await events.DoubleClickAsync(name, Token);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);
        Assert.Same(session.ClassVM.Class.EventGraphs.Single(g => g.Name == name), session.GraphVM.Graph);

        // Add a custom event via search: right click empty canvas, choose "Custom Event" (US4).
        var search = await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
        await search.FilterAsync("Custom Event", "Custom Event", Token);
        Assert.Contains(session.GraphVM.Search.Items, i => i.Text == "Custom Event" && i.Value is CustomEventSuggestion);
        await search.ChooseAsync("Custom Event", Token);
        await session.WaitForRenderedAsync(Token);

        var eventGraph = (EventGraph)session.GraphVM.Graph;
        Assert.Empty(session.App.Dialogs.Errors);
        var entry = Assert.Single(eventGraph.Nodes.OfType<EventEntryNode>());
        Assert.Equal("CustomEvent", entry.EventName); // unique against the class's methods and entries
        Assert.Equal(["EventEntryNode"], await session.Graph.NodeNamesAsync(Token));

        // Also add an override entry via search ("Override <method>", US4): ToString is overridable
        // on this class (OverrideChooserCreatesAndResets exercises the same base method).
        var overrideSearch = await (await session.Graph.RightClickAtAsync(400, 400, Token)).WaitOpenAsync(Token);
        await overrideSearch.FilterAsync("Override ToString", "Override ToString", Token);
        await overrideSearch.ChooseAsync("Override ToString", Token);
        await session.WaitForRenderedAsync(Token);

        var overrideEntry = eventGraph.Nodes.OfType<EventEntryNode>().Single(e => e.EventName == "ToString");
        Assert.Equal(MethodModifiers.Override, overrideEntry.Modifiers);
        Assert.NotNull(overrideEntry.OverriddenMethod);
        Assert.Equal(2, eventGraph.Nodes.OfType<EventEntryNode>().Count());

        // Remove (undoable): the list row's remove button has no automation id, matching the method
        // and variable rows (ClassEditorVMTests-style); drive the command directly, as those do.
        var eventGraphVM = session.ClassVM.EventGraphs.Single(g => g.Graph == eventGraph);
        session.ClassVM.RemoveEventGraphCommand.Execute(eventGraphVM);
        Assert.DoesNotContain(name, await events.EventGraphNamesAsync(Token));
        Assert.DoesNotContain(session.ClassVM.Class.EventGraphs, g => g.Name == name);
        Assert.Null(session.ClassVM.OpenedGraph); // the canvas showed the removed graph

        await session.ClassEditor.PressUndoAsync(Token);
        Assert.Contains(name, await events.EventGraphNamesAsync(Token));
        var restored = session.ClassVM.Class.EventGraphs.Single(g => g.Name == name);
        Assert.Same(eventGraph, restored); // undo restores the same instance, not a rebuilt one

        await session.ClassEditor.PressRedoAsync(Token);
        Assert.DoesNotContain(name, await events.EventGraphNamesAsync(Token));
    }
}
