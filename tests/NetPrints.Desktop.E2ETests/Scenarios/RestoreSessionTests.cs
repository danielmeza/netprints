using System.Text.RegularExpressions;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The layout, the open graphs and their viewports, and the window bounds come back after a restart (FR-050 to FR-052, SC-005).</summary>
public sealed partial class RestoreSessionTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string MainMethod = "Main";
    private const string ClassGraph = "Program";
    private const double PaneDrag = 90;
    private const double Tolerance = 0.01;
    private const double WindowMoveX = 40;
    private const double WindowMoveY = 30;
    private const double WindowWidth = 1000;
    private const double WindowHeight = 640;

    private static readonly DocumentId ClassDocument = DocumentId.Graph("HelloWorld.Program.netpc.json", DocumentId.ClassGraphKey);
    private static readonly DocumentId MainDocument = DocumentId.Graph("HelloWorld.Program.netpc.json", DocumentId.MethodKeyPrefix + "m000000001gs20");

    [GeneratedRegex(@"^\s*(graph:[^\s\[]+)", RegexOptions.Multiline)]
    private static partial Regex TabIds();

    private UiElement ProjectPanel => Shell.PanelContent(PanelContributions.ProjectTreeId);

    private async Task<IReadOnlyList<string>> OpenTabsAsync(CancellationToken cancellationToken) =>
        TabIds().Matches(await Driver.DumpAsync(cancellationToken)).Select(match => match.Groups[1].Value).Distinct().ToList();

    private async Task<(double X, double Y, double Zoom)> ViewportOfMainAsync(CancellationToken cancellationToken)
    {
        var (x, y) = await Shell.Graph.ViewportAsync(cancellationToken);
        return (x, y, await Shell.Graph.ZoomAsync(cancellationToken));
    }

    private async Task<(double X, double Y, double Width, double Height)> WindowBoundsAsync(CancellationToken cancellationToken)
    {
        var bounds = (await Shell.GetAsync(cancellationToken)).ScreenBounds;
        return (bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    [Fact]
    public Task LayoutTabsViewportAndWindowBoundsComeBackAfterARestart() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        byte[] original = await File.ReadAllBytesAsync(ClassFile, token);
        double paneWidth;
        IReadOnlyList<string> tabs;
        (double X, double Y, double Zoom) viewport;
        (double X, double Y, double Width, double Height) window;

        using (Step("move a pane"))
        {
            double before = (await ProjectPanel.GetAsync(token)).Bounds.Width;
            var edge = await ProjectPanel.OffsetAsync(before + 4, 200, token);
            await Driver.DragAsync(edge, edge.Offset(PaneDrag, 0), UiButton.Left, token);
            await ProjectPanel.WaitUntilAsync(e => e.Bounds.Width > before + PaneDrag / 2, "the pane wider", token);
        }

        using (Step("open three graphs"))
        {
            await Shell.Tree.SelectAsync(Shell.Tree.Class(ClassGraph), token);
            await Driver.PressAsync("Enter", token);
            await Shell.Graph.WaitForGraphAsync(ClassGraph, token);
            await Shell.Menu.InvokeAsync("Edit", ShellCommands.AddConstructor, token);
            await UiWait.UntilAsync(Driver, async () => (await OpenTabsAsync(token)).Count == 2, "the constructor graph open", token);
            await Shell.OpenMethodAsync(MainMethod, token);
            await UiWait.UntilAsync(Driver, async () => (await OpenTabsAsync(token)).Count == 3, "three graphs open", token);
            tabs = await OpenTabsAsync(token);
            await Shell.Menu.InvokeAsync("File", ShellCommands.SaveAll, token);
            await WaitForAsync(() => !original.AsSpan().SequenceEqual(File.ReadAllBytes(ClassFile)), "the constructor saved", token);
        }

        using (Step("zoom and pan one graph"))
        {
            await Shell.Tabs.SelectAsync(MainDocument, token);
            await Shell.Graph.WaitForGraphAsync(MainMethod, token);
            var (startX, startY, startZoom) = await ViewportOfMainAsync(token);
            await Shell.Graph.WheelAsync(await Shell.Graph.EmptyPointAsync(token), -3, token);
            await UiWait.UntilAsync(Driver, async () => await Shell.Graph.ZoomAsync(token) < startZoom, "zoomed out", token);
            await Shell.Graph.RightDragAsync(-120, -60, token);
            await UiWait.UntilAsync(Driver, async () => await Shell.Graph.ViewportAsync(token) != (startX, startY), "panned", token);
            await Shell.Tabs.SelectAsync(ClassDocument, token);
            await Shell.Graph.WaitForGraphAsync(ClassGraph, token);
            await Shell.Tabs.SelectAsync(MainDocument, token);
            await Shell.Graph.WaitForGraphAsync(MainMethod, token);
            viewport = await ViewportOfMainAsync(token);
            await Shell.Tabs.SelectAsync(ClassDocument, token);
            await Shell.Graph.WaitForGraphAsync(ClassGraph, token);
        }

        using (Step("move and resize the window"))
        {
            string id = (await Shell.GetAsync(token)).Window;
            await Driver.ResizeWindowAsync(id, WindowWidth, WindowHeight, token);
            await Driver.MoveWindowAsync(id, WindowMoveX, WindowMoveY, token);
            await Shell.WaitUntilAsync(e => Math.Abs(e.ScreenBounds.Width - WindowWidth) < 2, "the window resized", token);
            window = await WindowBoundsAsync(token);
            paneWidth = (await ProjectPanel.GetAsync(token)).Bounds.Width;
        }

        using (Step("close the editor and start it again"))
        {
            await CloseWindowAndWaitForExitAsync(token);
            await RestartEditorAsync(new EditorStart(Path.Combine(SampleDirectory, "HelloWorld.csproj"), Environment(), WaitForProject: true), token);
            await WaitForProjectAsync(token);
        }

        using (Step("the layout, tabs and active tab are back"))
        {
            await UiWait.UntilAsync(Driver, async () => (await OpenTabsAsync(token)).SequenceEqual(tabs), "the same tabs in the same order", token);
            Assert.True(await Shell.Tabs.IsSelectedAsync(ClassDocument, token), "the active tab is the class graph");
            Assert.False(await Shell.Tabs.IsSelectedAsync(MainDocument, token));
            await ProjectPanel.WaitUntilAsync(e => Math.Abs(e.Bounds.Width - paneWidth) <= 2, $"the pane {paneWidth:0} wide", token);
        }

        using (Step("the viewport and window bounds are back"))
        {
            await Shell.Tabs.SelectAsync(MainDocument, token);
            await Shell.Graph.WaitForGraphAsync(MainMethod, token);
            await UiWait.UntilAsync(Driver, async () => Near(await ViewportOfMainAsync(token), viewport), $"the viewport {viewport}", token);
            var restored = await WindowBoundsAsync(token);
            Assert.Equal(window.Width, restored.Width, 2);
            Assert.Equal(window.Height, restored.Height, 2);
            Assert.Equal(window.X, restored.X, 2);
            Assert.Equal(window.Y, restored.Y, 2);
        }
    });

    private static bool Near((double X, double Y, double Zoom) actual, (double X, double Y, double Zoom) expected) =>
        Math.Abs(actual.X - expected.X) < Tolerance && Math.Abs(actual.Y - expected.Y) < Tolerance && Math.Abs(actual.Zoom - expected.Zoom) < Tolerance;
}
