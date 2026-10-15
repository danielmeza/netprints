using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Commands;

/// <summary>Registered gestures run their commands from the window and from the scoped canvas and tree, never from a text input.</summary>
public class CommandKeyBindingTests
{
    private const string GlobalGesture = "Ctrl+Shift+B";

    public static TheoryData<string> ChordsOfATextField() => ["Ctrl+A", "Delete", "F2"];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGlobalGestureRunsWhereverTheFocusIs()
    {
        var handler = new ProbeHandler();
        using var host = new KeyHost(("global", GlobalGesture, CommandScope.Global, handler));

        host.Focus(host.OutsideText);
        host.Press(GlobalGesture);
        host.Focus(host.NodeText);
        host.Press(GlobalGesture);
        host.Focus(host.Canvas);
        host.Press(GlobalGesture);

        Assert.Equal(3, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGraphGestureRunsWhileTheCanvasHasFocus()
    {
        var handler = new ProbeHandler();
        using var host = new KeyHost(("graph", "Delete", CommandScope.Graph, handler));

        host.Focus(host.Canvas);
        host.Press("Delete");

        Assert.Equal(1, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGraphGestureDoesNotRunOutsideTheCanvas()
    {
        var handler = new ProbeHandler();
        using var host = new KeyHost(("graph", "Delete", CommandScope.Graph, handler));

        host.Focus(host.OutsideText);
        host.Press("Delete");
        host.Focus(host.Tree);
        host.Press("Delete");

        Assert.Equal(0, handler.Runs);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(ChordsOfATextField))]
    public void AGraphGestureStaysWithATextInputInsideANode(string chord)
    {
        var handler = new ProbeHandler();
        using var host = new KeyHost(("graph", chord, CommandScope.Graph, handler));

        host.Focus(host.NodeText);
        host.Press(chord);

        Assert.Equal(0, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADeleteInANodeTextBoxEditsTheText()
    {
        var handler = new ProbeHandler();
        using var host = new KeyHost(("graph", "Delete", CommandScope.Graph, handler));
        host.Focus(host.NodeText);
        host.NodeText.CaretIndex = 0;

        host.Press("Delete");

        Assert.Equal("ode", host.NodeText.Text);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AScopeRunsOnlyItsOwnCommands()
    {
        var graph = new ProbeHandler();
        var both = new ProbeHandler();
        var tree = new ProbeHandler();
        using var host = new KeyHost(
            ("graph", "Ctrl+G", CommandScope.Graph, graph),
            ("both", "Ctrl+H", CommandScope.Graph | CommandScope.ProjectTree, both),
            ("tree", "Ctrl+J", CommandScope.ProjectTree, tree));

        host.Focus(host.Tree);
        host.Press("Ctrl+G");
        host.Press("Ctrl+H");
        host.Press("Ctrl+J");
        host.Focus(host.Canvas);
        host.Press("Ctrl+G");
        host.Press("Ctrl+H");
        host.Press("Ctrl+J");

        Assert.Equal((1, 2, 1), (graph.Runs, both.Runs, tree.Runs));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADisabledCommandDoesNotRunAndTheStateIsReadAtInvocation()
    {
        var global = new ProbeHandler(enabled: false);
        var graph = new ProbeHandler(enabled: false);
        using var host = new KeyHost(("global", GlobalGesture, CommandScope.Global, global), ("graph", "Ctrl+G", CommandScope.Graph, graph));
        host.Focus(host.Canvas);

        host.Press(GlobalGesture);
        host.Press("Ctrl+G");
        Assert.Equal((0, 0), (global.Runs, graph.Runs));

        global.Enabled = true;
        graph.Enabled = true;
        host.Press(GlobalGesture);
        host.Press("Ctrl+G");

        Assert.Equal((1, 1), (global.Runs, graph.Runs));
    }
}
