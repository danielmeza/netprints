using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Graph;

public class GraphRenderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>ADR-0007 / batch X2a: NodeView's overload chooser moved off <c>x:CompileBindings="False"</c>;
    /// opening a graph with nodes (WriteLine's overload chooser included) must not log a binding warning.</summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RendersSampleMainGraphLogsNoBindingWarnings()
    {
        var sink = new BindingWarningLogSink();
        ILogSink? previousSink = Logger.Sink;
        try
        {
            await using var session = await EditorSession.OpenSampleMainAsync(Token, () => Logger.Sink = sink);
        }
        finally
        {
            Logger.Sink = previousSink;
        }

        // Dock's own theme templates log "Value is null" for their Layout and capability bindings; none comes from the NetPrints templates.
        Assert.All(sink.Messages, message => Assert.Matches("Layout\\.|DockCapability", message));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RendersSampleMainGraph()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        foreach (var node in session.GraphViewModel.Nodes)
        {
            Assert.Equal((node.Location.X, node.Location.Y), await session.Graph.Node(node.Node.Name).LocationAsync(Token));
        }

        Assert.Equal(["CallMethodNode.Exec->ReturnNode.Exec", "MethodEntryNode.Exec->CallMethodNode.Exec"],
            (await session.Graph.ConnectionNamesAsync(Token)).Order()); // entry -> WriteLine -> return
        Assert.All(session.GraphViewModel.Nodes.SelectMany(n => n.AllPins).Where(p => p.IsConnected),
            p => Assert.NotEqual(GraphPoint.Zero, p.Anchor)); // anchors pushed to the view models
        Assert.Equal("Main", await session.Graph.Watermark.TextAsync(Token)); // PAR-38
    }
}
