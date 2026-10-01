using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Events;

/// <summary>
/// OWN-07 (owner report): "Event graphs: when one is added they have the same problem the methods
/// had — slow to open and they don't open, only when focus is lost; and if I have one open and then
/// close it, the Main method won't open." Root cause: the event graph row opened on a double-click
/// (<c>ExecuteCommandOnDoubleTappedBehavior</c>) instead of the D1 single-tap pattern methods use.
/// Real clicks on the real rows, in the owner's exact sequence, not synthetic command calls.
/// </summary>
public class EventGraphOpenPipelineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OwnerSequenceAddEventGraphClickItClickMain()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var events = session.ClassEditor.EventGraphs;

        string name = await events.CreateAsync(Token); // also opens it
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);

        // A single click on the event graph row opens it immediately (it's already open from Create,
        // so this re-click must be a no-op that keeps it open, not a dead click).
        await events.OpenAsync(name, Token);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' still shown", Token);

        // A single click on Main must open Main (the owner's report: it didn't).
        await session.ClassEditor.Method("Main").ClickAsync(Token);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == "Main", "Main shown", Token);
        Assert.Same(session.ClassViewModel.Methods.Single(m => m.Name == "Main").Graph, session.ClassViewModel.OpenedGraph?.Graph);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SingleClickOnAnEventGraphOpensItWithoutADoubleClick()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var events = session.ClassEditor.EventGraphs;
        string name = await events.CreateAsync(Token);

        // Switch away, then a single click reopens it (matches methods since D1/F2).
        await session.ClassEditor.Method("Main").ClickAsync(Token);
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == "Main", "Main shown", Token);

        await events.OpenAsync(name, Token);

        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == name, $"graph '{name}' shown", Token);
        Assert.Same(session.ClassViewModel.EventGraphs.Single(g => g.Name == name).Graph, session.ClassViewModel.OpenedGraph?.Graph);
    }
}
