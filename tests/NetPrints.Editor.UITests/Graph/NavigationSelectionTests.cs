using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;

namespace NetPrints.Editor.UITests.Graph;

public class NavigationSelectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(EditorSession Session, NodeViewModel Write, NodeViewModel Return, ConnectionViewModel Cable)> OpenWithAStraightCableAsync()
    {
        var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var node = session.GraphViewModel.Nodes.Single(n => n.Node == method.MainReturnNode);
        node.Location = new GraphPoint(write.Location.X + 416, write.Location.Y);
        await session.WaitForRenderedAsync(Token);
        return (session, write, node, session.GraphViewModel.Connections.Single(c => c.Target.Node == node));
    }

    private static async Task CtrlClickAsync(EditorSession session, CableObject cable, double fraction)
    {
        UiTarget at = await cable.PointAlongAsync(fraction, Token);
        var point = new Avalonia.Point(at.X, at.Y);
        session.Window.MouseMove(point, RawInputModifiers.Control);
        session.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
        session.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
        HeadlessDriver.Pump();
    }

    private static Nodify.Avalonia.ItemContainer ContainerOf(EditorSession session, NodeViewModel node) =>
        session.Window.GetVisualDescendants().OfType<Nodify.Avalonia.ItemContainer>().Single(c => ReferenceEquals(c.DataContext, node));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task BackClearsTheCanvasSelectionOfTheNodeItLeaves()
    {
        var (session, _, node, cable) = await OpenWithAStraightCableAsync();
        await using (session)
        {
            await CtrlClickAsync(session, session.Graph.Connection(cable.AutomationName), 0.2);
            Assert.True(node.IsSelected);
            Assert.True(ContainerOf(session, node).IsSelected);

            session.App.Api.Navigation.GoBack();
            HeadlessDriver.Pump();

            Assert.False(node.IsSelected);
            Assert.False(ContainerOf(session, node).IsSelected);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AfterBackAClickOnTheNodeSelectsItAgain()
    {
        var (session, _, node, cable) = await OpenWithAStraightCableAsync();
        await using (session)
        {
            await CtrlClickAsync(session, session.Graph.Connection(cable.AutomationName), 0.2);
            session.App.Api.Navigation.GoBack();
            HeadlessDriver.Pump();

            await session.Graph.Node(node.Name).SelectAsync(Token);
            HeadlessDriver.Pump();

            Assert.True(node.IsSelected);
            Assert.Contains(node, session.GraphViewModel.SelectedNodes);
        }
    }
}
