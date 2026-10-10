using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>The overloads button of a call node and the flyout under it (FR-095, T092k).</summary>
public class OverloadFlyoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Button OverloadsButton(EditorSession session) =>
        session.Window.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == AutomationIds.NodeOverloads && button.IsEffectivelyVisible);

    private static async Task<MethodPickerList> OpenAsync(EditorSession session)
    {
        await session.Graph.Node("CallMethodNode").Overloads.ClickAsync(Token);
        var flyout = Assert.IsType<Flyout>(OverloadsButton(session).Flyout);
        Assert.True(flyout.IsOpen);
        return Assert.IsType<MethodPickerList>(flyout.Content);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheHeaderButtonHasAnAutomationIdAndAnAccessibleName()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        Button button = OverloadsButton(session);

        Assert.Equal("Overloads", AutomationProperties.GetName(button));
        Assert.True(button.IsEffectivelyVisible);
        Assert.StartsWith("Overloads (", Assert.IsType<string>(ToolTip.GetTip(button)), StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClickingTheButtonOpensAFlyoutWithTheFilterBoxFocused()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        MethodPickerList list = await OpenAsync(session);

        var filter = list.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetAutomationId(box) == AutomationIds.MethodPickerFilter);
        Assert.Same(filter, TopLevel.GetTopLevel(list)?.FocusManager?.GetFocusedElement());
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public async Task TheFlyoutFitsInsideTheWindowAtEveryZoom(double zoom)
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        Assert.IsType<GraphDocumentViewModel>(session.App.Shell.ActiveDocument).ViewportZoom = zoom;
        await session.WaitForRenderedAsync(Token);
        Assert.Equal(zoom, await session.Graph.ZoomAsync(Token), 3);
        Button button = OverloadsButton(session);
        button.Flyout?.ShowAt(button);
        var list = Assert.IsType<MethodPickerList>(Assert.IsType<Flyout>(button.Flyout).Content);
        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(list.Bounds.Height > 0), "the flyout laid out", Token);

        PixelPoint topLeft = list.PointToScreen(new Point(0, 0));
        PixelPoint bottomRight = list.PointToScreen(new Point(list.Bounds.Width, list.Bounds.Height));
        PixelPoint windowTopLeft = session.Window.PointToScreen(new Point(0, 0));
        PixelPoint windowBottomRight = session.Window.PointToScreen(new Point(session.Window.Bounds.Width, session.Window.Bounds.Height));
        Assert.True(list.Bounds.Height > 0);
        Assert.True(topLeft.X >= windowTopLeft.X && topLeft.Y >= windowTopLeft.Y, $"top left {topLeft} in {windowTopLeft}");
        Assert.True(bottomRight.X <= windowBottomRight.X && bottomRight.Y <= windowBottomRight.Y, $"bottom right {bottomRight} in {windowBottomRight}");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EscapeClosesTheFlyoutWithNoChange()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var before = method.Nodes.OfType<CallMethodNode>().Single();
        var undoName = session.ClassContext.UndoRedo.UndoName;
        MethodPickerList list = await OpenAsync(session);
        int cancels = 0;
        Assert.IsType<MethodPickerListViewModel>(list.DataContext).Cancelled += (_, _) => cancels++;

        await session.Driver.PressAsync("Esc", Token);

        Assert.Equal(1, cancels);
        Assert.False(Assert.IsType<Flyout>(OverloadsButton(session).Flyout).IsOpen);
        Assert.Same(before, method.Nodes.OfType<CallMethodNode>().Single());
        Assert.Equal(undoName, session.ClassContext.UndoRedo.UndoName);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PickingARowChangesTheOverloadAndClosesTheFlyout()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        MethodPickerList list = await OpenAsync(session);
        var picker = Assert.IsType<MethodPickerListViewModel>(list.DataContext);
        picker.Selected = picker.Rows.Single(row => row.Item?.Method is { Parameters: [{ Value: var type }] } && type == TypeSpecifier.FromType<int>());

        await session.Driver.PressAsync("Enter", Token);

        Assert.False(Assert.IsType<Flyout>(OverloadsButton(session).Flyout).IsOpen);
        Assert.Equal(TypeSpecifier.FromType<int>(), method.Nodes.OfType<CallMethodNode>().Single().MethodSpecifier.Parameters[0].Value);
    }
}
