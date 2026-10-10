using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Opening or activating a graph document puts focus in its canvas, including a tab switch that keeps the view (SC-004, Review B R5).</summary>
public class ShellGraphFocusTests
{
    private static readonly DocumentId First = DocumentId.Graph("C.cs", "method:1");
    private static readonly DocumentId Second = DocumentId.Graph("C.cs", "method:2");

    private static IInputElement? Focused(ShellRig rig) => TopLevel.GetTopLevel(rig.Main)?.FocusManager?.GetFocusedElement();

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAndSwitchingGraphTabsFocusesTheCanvasOfTheActiveGraph()
    {
        await using var app = HeadlessApp.Start();
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        using var context = new ClassContext(cls, app.Composition.Context, new UndoRedoStack());
        var graphs = new Dictionary<DocumentId, NodeGraphViewModel>
        {
            [First] = new NodeGraphViewModel(context.CreateMethod(), context.Services),
            [Second] = new NodeGraphViewModel(context.CreateMethod(), context.Services),
        };
        using var rig = ShellRig.Create(id => graphs.TryGetValue(id, out NodeGraphViewModel? graph) ? new GraphDocumentViewModel(id, graph, cls, session: null) : null);
        Assert.Equal(2, context.Methods.Count);

        rig.Api.OpenDocument(First);
        rig.Settle();
        AssertCanvasHasFocus(rig, graphs[First]);

        rig.Api.OpenDocument(Second);
        rig.Settle();
        AssertCanvasHasFocus(rig, graphs[Second]);

        rig.Api.ActivateDocument(First);
        rig.Settle();
        AssertCanvasHasFocus(rig, graphs[First]);

        rig.Api.ActivateDocument(Second);
        rig.Settle();
        AssertCanvasHasFocus(rig, graphs[Second]);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AGraphViewThatIsKeptWhileItsTabIsInactiveTakesTheFocusBackWhenItIsShownAgain()
    {
        await using var app = HeadlessApp.Start();
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        using var context = new ClassContext(cls, app.Composition.Context, new UndoRedoStack());
        var firstGraph = new NodeGraphViewModel(context.CreateMethod(), context.Services);
        var secondGraph = new NodeGraphViewModel(context.CreateMethod(), context.Services);
        var first = new GraphEditorView { DataContext = firstGraph };
        var second = new GraphEditorView { DataContext = secondGraph };
        var tabContent = new Panel { Children = { first } };
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(new Window { Width = 800, Height = 600, Content = tabContent });
        HeadlessDriver.Pump();
        var focus = () => TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement();
        Assert.Same(firstGraph, Assert.IsAssignableFrom<Control>(focus()).DataContext);

        tabContent.Children.Clear();
        tabContent.Children.Add(second);
        HeadlessDriver.Pump();
        Assert.Same(secondGraph, Assert.IsAssignableFrom<Control>(focus()).DataContext);

        tabContent.Children.Clear();
        tabContent.Children.Add(first);
        HeadlessDriver.Pump();
        Assert.Same(firstGraph, Assert.IsAssignableFrom<Control>(focus()).DataContext);
    }

    private static void AssertCanvasHasFocus(ShellRig rig, NodeGraphViewModel expected)
    {
        var focused = Assert.IsAssignableFrom<Control>(Focused(rig));
        Assert.Equal(AutomationIds.GraphEditor, Avalonia.Automation.AutomationProperties.GetAutomationId(focused));
        Assert.Same(expected, focused.DataContext);
    }
}
