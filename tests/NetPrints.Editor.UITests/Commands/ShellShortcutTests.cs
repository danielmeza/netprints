using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Commands;

/// <summary>The shell's Delete, Ctrl+Z and Ctrl+Y run through the command registry on the active graph.</summary>
public class ShellShortcutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TextBox ATextBoxOutsideTheCanvas(EditorSession session) =>
        session.Window.GetVisualDescendants().OfType<TextBox>().First(box => box.IsEffectivelyVisible && box.FindAncestorOfType<NetPrints.Editor.Graph.GraphEditorView>() is null);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteRunsFromTheCanvasThroughTheRegistry()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        int before = session.GraphViewModel.Nodes.Count;
        await session.Graph.Node("CallMethodNode").SelectAsync(Token);

        await session.PressDeleteAsync(Token);

        Assert.Equal(before - 1, session.GraphViewModel.Nodes.Count);
        Assert.DoesNotContain(session.GraphViewModel.Nodes, n => n.Node is CallMethodNode);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task UndoAndRedoActOnTheHistoryTheActiveClassRecordsTo()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var log = new List<string>();
        session.ClassContext.UndoRedo.Do(new DelegateUndoableCommand("Probe", () => log.Add("do"), () => log.Add("undo")));
        Assert.True(ATextBoxOutsideTheCanvas(session).Focus());

        await session.PressUndoAsync(Token);
        await session.PressRedoAsync(Token);

        Assert.Equal(["do", "undo", "do"], log);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteDoesNothingWhileTheFocusIsOutsideTheCanvas()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        int before = session.GraphViewModel.Nodes.Count;
        await session.Graph.Node("CallMethodNode").SelectAsync(Token);
        Assert.True(ATextBoxOutsideTheCanvas(session).Focus());

        await session.PressDeleteAsync(Token);

        Assert.Equal(before, session.GraphViewModel.Nodes.Count);
    }
}
