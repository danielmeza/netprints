using Avalonia.Headless;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;

namespace NetPrints.Editor.Tests.Ui;

[TestClass]
public class GraphRenderTests
{
    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task RendersSampleMainGraph() => UiTest.RunAsync(async () =>
    {
        string path = TestPaths.CopyHelloWorldSample();
        var (composition, mainWindow, _, _) = UiHelpers.StartEditor();
        try
        {
            var main = composition.MainEditor!;
            await main.LoadProjectAsync(path);
            var (window, editor) = await UiHelpers.OpenClassAsync(composition, main);
            Assert.AreEqual("Program", window.Title, "PAR-22");
            Assert.AreEqual(Avalonia.Controls.WindowState.Maximized, window.WindowState, "class windows open maximized (PAR-22)");

            editor.OpenMethodCommand.Execute(editor.Methods.Single());
            var graph = editor.OpenedGraph!;
            var view = window.Descendants<GraphEditorView>().Single();

            await UiTest.WaitUntilAsync(() => window.Descendants<ItemContainer>().Count() == graph.Nodes.Count);
            await UiTest.WaitUntilAsync(() => window.Descendants<Connection>().Count() == graph.Connections.Count);

            foreach (var container in window.Descendants<ItemContainer>())
            {
                var node = (NodeVM)container.DataContext!;
                Assert.AreEqual(node.Location.X, container.Location.X);
                Assert.AreEqual(node.Location.Y, container.Location.Y);
            }

            Assert.HasCount(2, graph.Connections, "entry -> WriteLine -> return");
            Assert.IsTrue(graph.Nodes.SelectMany(n => n.AllPins).Where(p => p.IsConnected).All(p => p.Anchor != GraphPoint.Zero),
                "connector anchors are pushed to the view models");

            Assert.AreEqual("Main", view.Find<Avalonia.Controls.TextBlock>("Watermark").Text, "PAR-38 watermark");
            Assert.IsNotNull(window.CaptureRenderedFrame());
        }
        finally
        {
            mainWindow.Close();
            TestPaths.TryDelete(path);
        }
    });
}
