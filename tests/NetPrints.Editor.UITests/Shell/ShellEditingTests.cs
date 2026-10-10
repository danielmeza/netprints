using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Editing the sample in the shell: the inspector, the edit keys, tooltips and activating an error row.</summary>
public class ShellEditingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheClassInspectorRenamesTheClassAsTypedAndShowsItsGeneratedCode()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        await inspector.ClassName.ClickAsync(Token);
        await session.Driver.PressAsync("Ctrl+A", Token);
        await session.Driver.TypeAsync("Renamed", Token);
        Assert.Equal("Renamed", session.Class.Name); // updates as typed

        // The code view follows CodeAnalysisHost's real-time debounce in this composition: wait for it instead of a virtual clock.
        await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Renamed"), "generated code", Token);
        Assert.Equal("True", await inspector.ClassCodeView.PropertyAsync(AutomationPropertyNames.IsReadOnly, Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteUndoRedoKeys()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;

        await session.AddVariableAsync(Token); // PAR-37
        Assert.Equal(["Variable"], session.ClassContext.Variables.Select(v => v.Name));
        await session.PressUndoAsync(Token);
        Assert.Empty(session.ClassContext.Variables);
        await session.PressRedoAsync(Token);
        Assert.Equal(["Variable"], session.ClassContext.Variables.Select(v => v.Name));

        await session.Graph.BoxSelectAllAsync(Token);
        await session.PressDeleteAsync(Token);
        await session.WaitForRenderedAsync(Token);

        Assert.DoesNotContain(method.Nodes, n => n is CallMethodNode);
        Assert.Equal(["MethodEntryNode", "ReturnNode"], (await session.Graph.NodeNamesAsync(Token)).Order()); // entry and main return are kept
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnUnsavedClassFileIsMarkedOnItsTabItsTreeRowAndTheWindowTitleUntilItIsSaved()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        DocumentId main = session.App.Shell.ActiveDocument?.Id ?? throw new InvalidOperationException("No document.");
        var tab = session.Page.Tabs.Tab(main);
        var row = session.Page.Tree.Class("Program");
        Assert.DoesNotContain('*', (await tab.GetAsync(Token)).Name ?? "");
        Assert.DoesNotContain('*', session.Window.Title ?? "");

        await session.AddVariableAsync(Token);

        await tab.WaitUntilAsync(e => (e.Name ?? "").EndsWith('*'), "the tab marked", Token);
        await row.WaitUntilAsync(e => e[AutomationPropertyNames.ItemStatus] == "Unsaved", "the tree row marked", Token);
        Assert.Equal("Program", (await row.GetAsync(Token)).Name);
        Assert.Contains("HelloWorld*", session.Window.Title, StringComparison.Ordinal);

        await session.RunAsync("save", Token);

        await tab.WaitUntilAsync(e => !(e.Name ?? "").EndsWith('*'), "the tab saved", Token);
        await row.WaitUntilAsync(e => string.IsNullOrEmpty(e[AutomationPropertyNames.ItemStatus]), "the tree row saved", Token);
        Assert.DoesNotContain("*", session.Window.Title, StringComparison.Ordinal);
        Assert.Equal("Saved 1 file(s)", session.App.Shell.StatusMessage);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteOnTheEmptyCanvasKeepsTheMethodBeingEdited()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var method = (MethodGraph)session.GraphViewModel.Graph;
        var document = session.App.Shell.ActiveDocument?.Id ?? throw new InvalidOperationException("No document.");

        await session.Graph.ClickEmptyAsync(Token);
        await session.PressDeleteAsync(Token);

        Assert.Contains(method, session.Class.Methods);
        Assert.Equal(document, session.App.Shell.ActiveDocument?.Id);
        Assert.Contains(document, session.App.Api.OpenDocuments);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheGlobalShortcutsWorkInAFloatedGraphWindow()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        DocumentId main = session.App.Shell.ActiveDocument?.Id ?? throw new InvalidOperationException("No document.");
        await session.RunAsync("floatDocument", Token);
        Assert.True(session.App.Api.IsFloating(main));
        var floated = session.Page.GraphOf(main);

        await floated.ClickEmptyAsync(Token);
        await session.AddVariableAsync(Token);
        Assert.Equal(["Variable"], session.ClassContext.Variables.Select(v => v.Name));

        await session.PressUndoAsync(Token);
        Assert.Empty(session.ClassContext.Variables);
        await session.PressRedoAsync(Token);
        Assert.Equal(["Variable"], session.ClassContext.Variables.Select(v => v.Name));

        await session.Driver.PressAsync("Ctrl+S", Token);
        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(!session.Class.IsDirty), "the class saved", Token);

        await session.Driver.PressAsync("Ctrl+W", Token);
        Assert.DoesNotContain(main, session.App.Api.OpenDocuments);
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

    /// <summary>A second method with a real CS1503 (<c>Guid.Parse(string)</c> fed an int), wired into
    /// its flow (same technique as SourceMapTests/CodeAnalysisHostTests): the graph model does not itself
    /// enforce pin type compatibility.</summary>
    internal static (MethodGraph Method, CallMethodNode CallNode) AddBadCallMethod(ClassGraph cls)
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

    private static async Task<(EditorSession Session, MethodGraph Method, CallMethodNode CallNode)> StartWithAnErrorAsync()
    {
        var session = await EditorSession.OpenSampleMainAsync(Token);
        var (method, callNode) = AddBadCallMethod(session.Class);
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(session.App.Session.Project);
        await session.Page.Bottom.ShowAsync(PanelContributions.ErrorsId, Token);
        await session.Page.Bottom.ErrorRow().WaitVisibleAsync(Token, TimeSpan.FromSeconds(30));
        return (session, method, callNode);
    }

    private static Task WaitForErrorGraphAsync(EditorSession session, MethodGraph method, CallMethodNode callNode) =>
        UiWait.UntilAsync(session.Driver,
            () => Task.FromResult(session.App.Shell.ActiveDocument is GraphDocumentViewModel { Graph: { } shown } && shown.Graph == method
                && shown.SelectedNodes.Any(n => n.Node == callNode)),
            "the method with the error open and the failing node selected", Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleTappingAnErrorRowsBackgroundOpensItsGraphAndSelectsTheNode()
    {
        // OWN-04: only rendered text was hit-testable in a row; double-clicking its empty space did nothing.
        var (session, method, callNode) = await StartWithAnErrorAsync();
        await using var _ = session;
        var row = session.Page.Bottom.ErrorRow();
        var bounds = (await row.GetAsync(Token)).Bounds;

        await session.Driver.ClickAsync(await row.OffsetAsync(bounds.Width - 6, bounds.Height / 2, Token), UiButton.Left, 2, Token);

        await WaitForErrorGraphAsync(session, method, callNode);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PressingEnterOnTheSelectedErrorRowNavigatesToo()
    {
        // OWN-04 keyboard a11y: Enter on the selected row navigates the same as a double-click.
        var (session, method, callNode) = await StartWithAnErrorAsync();
        await using var _ = session;

        await session.Page.Bottom.ErrorRow().ClickAsync(Token); // selects the row
        await session.Driver.PressAsync("Enter", Token);

        await WaitForErrorGraphAsync(session, method, callNode);
    }
}
