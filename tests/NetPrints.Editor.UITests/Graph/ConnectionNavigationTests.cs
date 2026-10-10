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

/// <summary>Ctrl+click and the context menu of a connection take the user to one of its ends (FR-062, US7 scenario 3).</summary>
public class ConnectionNavigationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(EditorSession Session, NodeViewModel Write, NodeViewModel Return, ConnectionViewModel Cable)> OpenWithAStraightCableAsync()
    {
        var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var ret = session.GraphViewModel.Nodes.Single(n => n.Node == method.MainReturnNode);
        ret.Location = new GraphPoint(write.Location.X + 560, write.Location.Y);
        await session.WaitForRenderedAsync(Token);
        return (session, write, ret, session.GraphViewModel.Connections.Single(c => c.Target.Node == ret));
    }

    private static async Task ClickAsync(EditorSession session, CableObject cable, double fraction, KeyModifiers keys, MouseButton button)
    {
        UiTarget at = await cable.PointAlongAsync(fraction, Token);
        var point = new Avalonia.Point(at.X, at.Y);
        RawInputModifiers raw = keys == KeyModifiers.Control ? RawInputModifiers.Control : RawInputModifiers.None;
        session.Window.MouseMove(point, raw);
        session.Window.MouseDown(point, button, raw);
        session.Window.MouseUp(point, button, raw);
        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlClickOnACableGoesToTheEndFartherFromTheClickAndBackComesBack()
    {
        var (session, write, ret, cable) = await OpenWithAStraightCableAsync();
        await using (session)
        {
            var document = Assert.IsType<NetPrints.Editor.Shell.GraphDocumentViewModel>(session.App.Shell.ActiveDocument);
            var before = document.ViewportLocation;
            CableObject view = session.Graph.Connection(cable.AutomationName);

            await ClickAsync(session, view, 0.2, KeyModifiers.Control, MouseButton.Left);

            Assert.Same(ret, Assert.Single(session.GraphViewModel.SelectedNodes));
            Assert.NotEqual(before, document.ViewportLocation);
            Assert.True(session.App.Api.Navigation.CanGoBack);

            session.App.Api.Navigation.GoBack();
            HeadlessDriver.Pump();
            Assert.Equal(before, document.ViewportLocation);
            Assert.DoesNotContain(ret, session.GraphViewModel.SelectedNodes);

            await ClickAsync(session, view, 0.6, KeyModifiers.Control, MouseButton.Left);

            Assert.Same(write, Assert.Single(session.GraphViewModel.SelectedNodes));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AClickWithoutCtrlDoesNotNavigate()
    {
        var (session, _, _, cable) = await OpenWithAStraightCableAsync();
        await using (session)
        {
            await ClickAsync(session, session.Graph.Connection(cable.AutomationName), 0.2, KeyModifiers.None, MouseButton.Left);

            Assert.False(session.App.Api.Navigation.CanGoBack);
            Assert.Empty(session.GraphViewModel.SelectedNodes);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheContextMenuOfACableOffersGoToSourceAndGoToTargetNamingTheNodeAndThePin()
    {
        var (session, write, ret, cable) = await OpenWithAStraightCableAsync();
        await using (session)
        {
            await ClickAsync(session, session.Graph.Connection(cable.AutomationName), 0.5, KeyModifiers.None, MouseButton.Right);

            ContextMenu menu = session.Window.GetVisualDescendants().OfType<Nodify.Avalonia.Connections.Connection>().Single(c => ReferenceEquals(c.DataContext, cable)).ContextMenu
                ?? throw new InvalidOperationException("The cable has no context menu.");
            Assert.True(menu.IsOpen);
            MenuItem[] items = [.. Enumerable.Range(0, menu.ItemCount).Select(i => Assert.IsType<MenuItem>(menu.ContainerFromIndex(i)))];
            Assert.Equal([$"Go to source ({cable.Source.Node.Name}.{cable.Source.Pin.Name})", $"Go to target ({cable.Target.Node.Name}.{cable.Target.Pin.Name})"], items.Select(item => item.Header as string));
            Assert.False(session.GraphViewModel.Search.IsOpen);

            items[1].Command?.Execute(null);
            HeadlessDriver.Pump();

            Assert.Same(ret, Assert.Single(session.GraphViewModel.SelectedNodes));
            Assert.True(session.App.Api.Navigation.CanGoBack);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheTooltipOfACableNamesItsEndsItsTypeAndTheDocumentationOfTheCalledMethod()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode);
        var cable = session.GraphViewModel.Connections.Single(c => c.Target.Node == write && c.Kind == PinKind.Exec);

        var view = session.Window.GetVisualDescendants().OfType<Nodify.Avalonia.Connections.Connection>().Single(c => ReferenceEquals(c.DataContext, cable));
        string tip = Assert.IsType<string>(ToolTip.GetTip(view));

        Assert.StartsWith($"{cable.Source.Node.Name}.{cable.Source.Pin.Name} → {write.Name}.{cable.Target.Pin.Name}", tip, StringComparison.Ordinal);
        Assert.Contains("execution", tip, StringComparison.Ordinal);
        Assert.Contains("standard output stream", tip, StringComparison.Ordinal);
    }
}
