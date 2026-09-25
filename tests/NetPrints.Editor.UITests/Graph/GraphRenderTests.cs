using Avalonia.Headless.XUnit;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Graph;

public class GraphRenderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RendersSampleMainGraph()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);

        Assert.Equal("Program", await session.ClassEditor.TextAsync(Token)); // PAR-22
        foreach (var node in session.GraphVM.Nodes)
        {
            Assert.Equal((node.Location.X, node.Location.Y), await session.Graph.Node(node.Node.Name).LocationAsync(Token));
        }

        Assert.Equal(["CallMethodNode.Exec->ReturnNode.Exec", "MethodEntryNode.Exec->CallMethodNode.Exec"],
            (await session.Graph.ConnectionNamesAsync(Token)).Order()); // entry -> WriteLine -> return
        Assert.All(session.GraphVM.Nodes.SelectMany(n => n.AllPins).Where(p => p.IsConnected),
            p => Assert.NotEqual(GraphPoint.Zero, p.Anchor)); // anchors pushed to the view models
        Assert.Equal("Main", await session.Graph.Watermark.TextAsync(Token)); // PAR-38
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClassWindowsOpenMaximized()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);

        var page = await app.Main.OpenClassAsync("HelloWorld.Program", Token);

        Assert.Equal("Maximized", await page.WindowStateAsync(Token)); // PAR-22
    }
}
