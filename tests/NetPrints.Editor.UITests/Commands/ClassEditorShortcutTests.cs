using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Commands;

/// <summary>The class editor window's Delete, Ctrl+Z and Ctrl+Y run through the command registry.</summary>
public class ClassEditorShortcutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteRunsFromTheCanvasThroughTheRegistry()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        Assert.NotNull(session.ClassViewModel.Commands);
        int before = session.GraphViewModel.Nodes.Count;
        await session.Graph.Node("CallMethodNode").SelectAsync(Token);

        await session.ClassEditor.PressDeleteAsync(Token);

        Assert.Equal(before - 1, session.GraphViewModel.Nodes.Count);
        Assert.DoesNotContain(session.GraphViewModel.Nodes, n => n.Node is CallMethodNode);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task UndoAndRedoActOnTheHistoryTheEditorRecordsTo()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var log = new List<string>();
        session.ClassViewModel.UndoRedo.Do(new DelegateUndoableCommand("Probe", () => log.Add("do"), () => log.Add("undo")));
        var list = session.ClassWindow.FindControl<ComboBox>("OverrideBox");
        Assert.NotNull(list);
        Assert.True(list.Focus());

        await session.ClassEditor.PressUndoAsync(Token);
        await session.ClassEditor.PressRedoAsync(Token);

        Assert.Equal(["do", "undo", "do"], log);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteDoesNothingWhileTheFocusIsOutsideTheCanvas()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        int before = session.GraphViewModel.Nodes.Count;
        await session.Graph.Node("CallMethodNode").SelectAsync(Token);
        var list = session.ClassWindow.FindControl<ComboBox>("OverrideBox");
        Assert.NotNull(list);
        Assert.True(list.Focus());

        await session.ClassEditor.PressDeleteAsync(Token);

        Assert.Equal(before, session.GraphViewModel.Nodes.Count);
    }
}
