using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;

namespace NetPrints.Editor.UITests.Graph;

public class GraphRenderTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RendersSampleMainGraph()
    {
        using var session = await EditorSession.OpenSampleMainAsync();
        var graph = session.Graph;

        Assert.Equal("Program", session.ClassEditor.Window.Title); // PAR-22
        foreach (var node in graph.ViewModel.Nodes)
        {
            var container = graph.ContainerOf(node);
            Assert.Equal(node.Location.X, container.Location.X);
            Assert.Equal(node.Location.Y, container.Location.Y);
        }

        Assert.Equal(2, graph.ViewModel.Connections.Count); // entry -> WriteLine -> return
        Assert.All(graph.ViewModel.Nodes.SelectMany(n => n.AllPins).Where(p => p.IsConnected),
            p => Assert.NotEqual(GraphPoint.Zero, p.Anchor)); // anchors pushed to the view models
        Assert.Equal("Main", graph.Watermark); // PAR-38
        Assert.NotNull(session.ClassEditor.Window.CaptureRenderedFrame());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClassWindowsOpenMaximized()
    {
        using var sample = new Hosting.SampleCopy();
        using var main = Main.MainWindowPage.Start();
        await main.OpenStartupProjectAsync(sample.ProjectPath);

        var page = await main.OpenClassAsync("HelloWorld.Program");

        Assert.Equal(Avalonia.Controls.WindowState.Maximized, page.Window.WindowState); // PAR-22
    }
}
