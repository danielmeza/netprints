using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.ClassEditor;

public class ClassEditorWindowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Batch D1 (owner-reported log noise): records every Avalonia "Binding" area message at
    /// Warning or above, the way opening the inspectors used to log "Value is null" for
    /// <c>$parent[Window].DataContext.Show*Inspector</c> before the window set its DataContext before
    /// loading its XAML (assembly-wide sequential tests, TestAppBuilder.cs, so a global sink is safe).</summary>
    private sealed class BindingWarningLogSink : ILogSink
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == "Binding";

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Messages.Add(messageTemplate);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Messages.Add($"{messageTemplate} {string.Join(' ', propertyValues)}");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAClassAndAMethodLogsNoBindingWarnings()
    {
        var sink = new BindingWarningLogSink();
        ILogSink? previousSink = Logger.Sink;
        Logger.Sink = sink;
        try
        {
            await using var session = await EditorSession.OpenSampleMainAsync(Token);
        }
        finally
        {
            Logger.Sink = previousSink;
        }

        Assert.Empty(sink.Messages);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task InspectorsListsAndGeneratedCode()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var vm = session.ClassVM;

        foreach (var splitter in page.Splitters)
        {
            Assert.True(await splitter.ExistsAsync(Token), $"{splitter} exists"); // PAR-31
        }
        Assert.Equal("True", await page.CompileButton.PropertyAsync(AutomationPropertyNames.ShowToolTipOnDisabled, Token));

        // Opening Main by double click: the first click already showed the method inspector (PAR-24, 33).
        Assert.True(await page.MethodInspector.IsVisibleAsync(Token));
        Assert.Equal(InspectorKind.Method, vm.Inspector);
        Assert.False(await page.ClassInspector.IsVisibleAsync(Token));

        await page.CreateVariableButton.ClickAsync(Token); // PAR-29, 30
        await page.VariableNameText("Variable").ClickAsync(Token);
        await page.VariableInspector.WaitVisibleAsync(Token);

        await page.ClassButton.ClickAsync(Token); // PAR-23, 34
        Assert.Same(vm.Class, vm.OpenedGraph?.Graph);
        await page.ClassInspector.WaitVisibleAsync(Token);
        await page.ClassInspector.NameBox.ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync("Renamed", Token);
        Assert.Equal("Renamed", vm.Class.Name); // updates as typed
        page = new NetPrints.Testing.Ui.ClassEditor.ClassEditorPage(session.Driver, vm.Class.FullName);
        Assert.Equal("Renamed", await page.TextAsync(Token)); // window title

        // The code view follows CodeAnalysisHost's real-time debounce in this composition (editor-services.md
        // §2): wait for it instead of a virtual clock.
        await page.ClassInspector.CodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Renamed"), "generated code", Token);
        Assert.Equal("True", await page.ClassInspector.CodeView.PropertyAsync(AutomationPropertyNames.IsReadOnly, Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OverrideChooserCreatesAndResets()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var toString = session.ClassVM.OverridableMethods.First(m => m.Name == "ToString"); // PAR-26

        session.ClassWindow.FindControl<ComboBox>("OverrideBox")!.SelectedItem = toString; // choosing a combo box item
        await session.ClassEditor.OverrideChooser.WaitUntilAsync(e => e[AutomationPropertyNames.SelectedItem] is null, "chooser reset", Token);

        Assert.Contains("ToString", await session.ClassEditor.MethodNamesAsync(Token));
        await session.Graph.Watermark.WaitUntilAsync(e => e.Text == "ToString", "ToString opened", Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteUndoRedoKeys()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var method = (MethodGraph)session.GraphVM.Graph;

        await page.CreateVariableButton.ClickAsync(Token); // PAR-37
        Assert.Equal(["Variable"], await page.VariableNamesAsync(Token));
        await page.PressUndoAsync(Token);
        Assert.Empty(await page.VariableNamesAsync(Token));
        await page.PressRedoAsync(Token);
        Assert.Equal(["Variable"], await page.VariableNamesAsync(Token));

        await session.Graph.BoxSelectAllAsync(Token);
        await page.PressDeleteAsync(Token);
        await session.WaitForRenderedAsync(Token);

        Assert.DoesNotContain(method.Nodes, n => n is CallMethodNode);
        Assert.Equal(["MethodEntryNode", "ReturnNode"], (await session.Graph.NodeNamesAsync(Token)).Order()); // entry and main return are kept
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RunCompilesStartsAndPrints()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        await session.ClassEditor.RunButton.ClickAsync(Token);

        Assert.Equal("Build succeeded", await session.ClassEditor.WaitForBuildResultAsync(Token)); // PAR-10, PAR-32
        Assert.Contains("Hello, World!", await session.ClassEditor.WaitForOutputContainingAsync("Hello, World!", Token)); // through the Output pane, not the editor's own terminal (D3)
        Assert.Single(session.App.Processes.Started);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task BrokenGraphFillsTheErrorList()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        await session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox.ClickAsync(UiButton.Middle, Token); // clear "Hello, World!"

        string status = await session.ClassEditor.CompileAsync(Token); // PAR-10, PAR-32

        Assert.Equal("Build failed with 1 error(s)", status);
        await session.ClassEditor.ErrorList.WaitUntilAsync(e => e[AutomationPropertyNames.ItemCount] == "1", "one error listed", Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NodesAndPinsHaveToolTips()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var write = session.Graph.Node("CallMethodNode");

        foreach (string pin in await write.PinNamesAsync(Token))
        {
            var element = await write.Pin(pin).GetAsync(Token);
            Assert.False(string.IsNullOrWhiteSpace(element[AutomationPropertyNames.ToolTip]), $"pin {pin} has a tool tip (PAR-39, PAR-43)");
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SplittersResizeTheirNeighbours()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;

        async Task<(double Width, double Height)> SizeAsync(UiElement e)
        {
            var element = await e.GetAsync(Token);
            return (element.Bounds.Width, element.Bounds.Height);
        }

        var left = await SizeAsync(page.LeftColumn);
        await page.LeftSplitter.DragByAsync(80, 0, Token); // PAR-31
        Assert.Equal(left.Width + 80, (await SizeAsync(page.LeftColumn)).Width, 2.0);

        var inspector = await SizeAsync(page.InspectorColumn);
        await page.InspectorSplitter.DragByAsync(-60, 0, Token);
        Assert.Equal(inspector.Width + 60, (await SizeAsync(page.InspectorColumn)).Width, 2.0);

        var methods = await SizeAsync(page.MethodList);
        await page.MethodsSplitter.DragByAsync(0, 40, Token);
        Assert.Equal(methods.Height + 40, (await SizeAsync(page.MethodList)).Height, 2.0);

        var constructors = await SizeAsync(page.ConstructorList);
        await page.ConstructorsSplitter.DragByAsync(0, 30, Token);
        Assert.Equal(constructors.Height + 30, (await SizeAsync(page.ConstructorList)).Height, 2.0);

        var errors = await SizeAsync(page.ErrorList);
        await page.ErrorsSplitter.DragByAsync(0, -50, Token);
        Assert.Equal(errors.Height + 50, (await SizeAsync(page.ErrorList)).Height, 2.0);
    }
}
