using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.ClassEditor;

public class ClassEditorWindowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningAClassAndAMethodLogsNoBindingWarnings()
    {
        var sink = new BindingWarningLogSink();
        ILogSink? previousSink = Logger.Sink;
        try
        {
            // The sink goes in once the app exists: the shell window it composes logs the dock control's own startup warnings.
            await using var session = await EditorSession.OpenSampleMainAsync(Token, () => Logger.Sink = sink);
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
        var vm = session.ClassViewModel;

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
        var toString = session.ClassViewModel.OverridableMethods.First(m => m.Name == "ToString"); // PAR-26

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
        var method = (MethodGraph)session.GraphViewModel.Graph;

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
        var valuePin = session.GraphViewModel.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        await session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox.ClickAsync(UiButton.Middle, Token); // clear "Hello, World!"

        string status = await session.ClassEditor.CompileAsync(Token); // PAR-10, PAR-32

        Assert.Equal("Build failed with 1 error(s)", status);
        await session.ClassEditor.ErrorList.WaitUntilAsync(e => e[AutomationPropertyNames.ItemCount] == "1", "one error listed", Token);
    }

    /// <summary>A second method with a real CS1503 (<c>Guid.Parse(string)</c> fed an int), wired into
    /// its flow (same technique as SourceMapTests/CodeAnalysisHostTests and
    /// <see cref="CodeView.CodeViewTests.DoubleClickingADiagnosticRowOpensTheGraphSelectsAndRevealsTheNode"/>):
    /// the graph model does not itself enforce pin type compatibility.</summary>
    private static (MethodGraph Method, CallMethodNode CallNode) AddBadCallMethod(ClassGraph cls)
    {
        var method = new MethodGraph("BadCall") { Class = cls, Visibility = MemberVisibility.Public };
        TypeSpecifier stringType = TypeSpecifier.FromType<string>();
        var parseSpecifier = new MethodSpecifier("Parse",
            [new MethodParameter("input", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<Guid>(), []);
        var callNode = new CallMethodNode(method, parseSpecifier);
        var badArgument = LiteralNode.WithValue(method, 123);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, callNode.InputExecPins[0]);
        GraphUtil.ConnectExecPins(callNode.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
        GraphUtil.ConnectDataPins(badArgument.ValuePin, callNode.ArgumentPins[0]);
        cls.Methods.Add(method);
        return (method, callNode);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleTappingAnErrorRowsBackgroundOpensItsGraphAndSelectsTheNode()
    {
        // OWN-04 (owner-reproduced, FR-034/ED-T03): the row's StackPanel had no Background, so only
        // the rendered text glyphs (e.g. what ErrorRow("CS1503") itself clicks) were hit-testable;
        // double-clicking elsewhere in the row (the gap right after the severity icon, inside its
        // Spacing="8") used to do nothing.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var vm = session.ClassViewModel;
        var (method, callNode) = AddBadCallMethod(vm.Class);

        Project project = vm.Project ?? throw new InvalidOperationException("Expected the opened class to have a project.");
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(project);
        await page.ErrorRow("CS1503").WaitVisibleAsync(Token, TimeSpan.FromSeconds(30));

        var icon = page.ErrorSeverityIcon(0);
        var gap = await icon.OffsetAsync(20, 8, Token); // 4px past the 16px-wide icon, inside its Spacing="8" gap: row background, not text
        await session.Driver.ClickAsync(gap, UiButton.Left, 2, Token);

        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(vm.OpenedGraph?.Graph == method), "the method with the error to open", Token);
        NodeGraphViewModel openedGraph = vm.OpenedGraph ?? throw new InvalidOperationException("Expected a graph to be open.");
        Assert.Contains(openedGraph.SelectedNodes, n => n.Node == callNode);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PressingEnterOnTheSelectedErrorRowNavigatesToo()
    {
        // OWN-04 keyboard a11y: Enter on the selected row navigates the same as a double-click.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var vm = session.ClassViewModel;
        var (method, callNode) = AddBadCallMethod(vm.Class);

        Project project = vm.Project ?? throw new InvalidOperationException("Expected the opened class to have a project.");
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(project);
        await page.ErrorRow("CS1503").WaitVisibleAsync(Token, TimeSpan.FromSeconds(30));

        await page.ErrorRow("CS1503").ClickAsync(Token); // selects the row
        await session.Driver.PressAsync("Enter", Token);

        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(vm.OpenedGraph?.Graph == method), "the method with the error to open", Token);
        NodeGraphViewModel openedGraph = vm.OpenedGraph ?? throw new InvalidOperationException("Expected a graph to be open.");
        Assert.Contains(openedGraph.SelectedNodes, n => n.Node == callNode);
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
