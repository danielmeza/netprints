using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Events;

/// <summary>Editing an argument in the event entry inspector keeps the keyboard focus and commits on Enter (Review F R7).</summary>
public class EventArgumentEditingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(EditorSession Session, EventEntryNode Entry)> OpenInspectorWithAnArgumentAsync()
    {
        var session = await EditorSession.OpenSampleMainAsync(Token);
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
        await session.Page.Inspector.EventEntryAddArgument.ClickAsync(Token);
        await session.Page.Inspector.EventEntryArgumentName.WaitVisibleAsync(Token);
        return (session, entry);
    }

    private static async Task TypeNameAsync(EditorSession session, string name)
    {
        await session.Page.Inspector.EventEntryArgumentName.ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync(name, Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TabAfterRenamingAnArgumentKeepsKeyboardFocusInTheInspector()
    {
        var (session, entry) = await OpenInspectorWithAnArgumentAsync();
        await using (session)
        {
            await TypeNameAsync(session, "amount");
            await session.Driver.PressAsync("Tab", Token);
            HeadlessDriver.Pump();

            Assert.Equal("amount", entry.Arguments.Single().Name);
            var focused = TopLevel.GetTopLevel(session.Window)?.FocusManager?.GetFocusedElement() as Avalonia.Visual;
            Assert.NotNull(focused);
            Assert.True(focused.GetVisualAncestors().OfType<EventEntryInspectorView>().Any(), "focus left the inspector");
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterInTheArgumentNameCommitsIt()
    {
        var (session, entry) = await OpenInspectorWithAnArgumentAsync();
        await using (session)
        {
            await TypeNameAsync(session, "amount");
            await session.Driver.PressAsync("Enter", Token);
            HeadlessDriver.Pump();

            Assert.Equal("amount", entry.Arguments.Single().Name);
        }
    }
}
