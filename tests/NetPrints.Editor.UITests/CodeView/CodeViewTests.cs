using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.CodeView;

/// <summary>
/// The class inspector's read-only C# code view (US6, FR-031..035): ED-T01 (highlighting, line
/// numbers, no wrap, folding), ED-T03 (navigation) and ED-T05 (hover quick info). Live-analysis
/// diagnostics and the debounce are covered at the host level by
/// <see cref="Editor.Tests.Diagnostics.CodeAnalysisHostTests"/> (ED-T02).
/// </summary>
public class CodeViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SnapshotStore Store => UiArtifacts.Snapshots;

    /// <summary>The real <see cref="NetPrints.Editor.CodeView.CodeView"/> control, found by its type
    /// rather than by name scope (it is nested inside <c>ClassInspectorView</c>'s own scope).</summary>
    private static NetPrints.Editor.CodeView.CodeView FindCodeView(EditorSession session) =>
        session.ClassWindow.GetVisualDescendants().OfType<NetPrints.Editor.CodeView.CodeView>().Single();

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CodeViewIsHighlightedNumberedFoldedAndDoesNotWrap()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;

        await page.ClassButton.ClickAsync(Token);
        await page.ClassInspector.WaitVisibleAsync(Token);
        await page.ClassInspector.CodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Program", StringComparison.Ordinal), "generated code", Token);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        Assert.True(codeView.CodeEditor.ShowLineNumbers);
        Assert.False(codeView.CodeEditor.WordWrap);
        Assert.NotEmpty(codeView.ViewModel!.Foldings); // at least the class and the Main method

        // ED-T01's other half (highlighted tokens) is verified visually: reviewed on regeneration.
        Store.Match("class-inspector-code-view", await page.InspectorColumn.ScreenshotAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringWriteLineShowsSignatureAndSummary()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;

        await page.ClassButton.ClickAsync(Token);
        await page.ClassInspector.WaitVisibleAsync(Token);
        await page.ClassInspector.CodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("WriteLine", StringComparison.Ordinal), "generated code", Token);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        string code = codeView.ViewModel!.Code;
        // "WriteLine(", not "WriteLine": the generated code also has a "// Console.WriteLine" comment above the call.
        int offset = code.IndexOf("WriteLine(", StringComparison.Ordinal) + 2;

        await codeView.ShowQuickInfoAsync(offset, Token);

        // OWN-01 (owner report): Avalonia only auto-opens a tooltip on its own pointer-enter, never
        // when the tip is merely set programmatically, so asserting the tip's text alone (as this test
        // used to) does not catch a tooltip that is set but never shown.
        bool isOpen = await page.ClassInspector.CodeView.GetAsync<bool>(AutomationPropertyNames.ToolTipIsOpen, Token);
        Assert.True(isOpen, "the tooltip is open");
        string? tip = await page.ClassInspector.CodeView.PropertyAsync(AutomationPropertyNames.ToolTip, Token);
        Assert.Contains("WriteLine", tip ?? "", StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringADiagnosticSpanShowsItAboveTheQuickInfo()
    {
        // OWN-02 (owner report): hovering a squiggle shows the diagnostic(s) under the cursor, above
        // the symbol quick info. Same CS1503 setup as
        // DoubleClickingADiagnosticRowOpensTheGraphSelectsAndRevealsTheNode.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var vm = session.ClassVM;

        var method = new MethodGraph("BadCall") { Class = vm.Class, Visibility = MemberVisibility.Public };
        TypeSpecifier stringType = TypeSpecifier.FromType<string>();
        var parseSpecifier = new MethodSpecifier("Parse",
            [new MethodParameter("input", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<Guid>(), []);
        var callNode = new CallMethodNode(method, parseSpecifier);
        var badArgument = LiteralNode.WithValue(method, 123);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, callNode.InputExecPins[0]);
        GraphUtil.ConnectExecPins(callNode.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
        GraphUtil.ConnectDataPins(badArgument.ValuePin, callNode.ArgumentPins[0]);
        vm.Class.Methods.Add(method);

        await page.ClassButton.ClickAsync(Token);
        await page.ClassInspector.WaitVisibleAsync(Token);
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(vm.Project!);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(codeView.ViewModel!.Diagnostics.Any(d => d.Id == "CS1503")),
            "the CS1503 diagnostic to reach the code view", Token, TimeSpan.FromSeconds(30));

        CodeDiagnostic diagnostic = codeView.ViewModel!.Diagnostics.First(d => d.Id == "CS1503");
        LinePositionSpan span = diagnostic.Span!.Value;
        int offset = SourceText.From(codeView.ViewModel!.Code).Lines.GetPosition(span.Start);

        await codeView.ShowQuickInfoAsync(offset, Token);

        bool isOpen = await page.ClassInspector.CodeView.GetAsync<bool>(AutomationPropertyNames.ToolTipIsOpen, Token);
        Assert.True(isOpen, "the tooltip is open");
        string? tip = await page.ClassInspector.CodeView.PropertyAsync(AutomationPropertyNames.ToolTip, Token);
        Assert.Contains("CS1503", tip ?? "", StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingADiagnosticRowOpensTheGraphSelectsAndRevealsTheNode()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;
        var vm = session.ClassVM;

        // A second method with a real CS1503 (Guid.Parse(string) fed an int), wired into its flow
        // (same technique as SourceMapTests/CodeAnalysisHostTests): the graph model does not itself
        // enforce pin type compatibility.
        var method = new MethodGraph("BadCall") { Class = vm.Class, Visibility = MemberVisibility.Public };
        TypeSpecifier stringType = TypeSpecifier.FromType<string>();
        var parseSpecifier = new MethodSpecifier("Parse",
            [new MethodParameter("input", stringType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType<Guid>(), []);
        var callNode = new CallMethodNode(method, parseSpecifier);
        var badArgument = LiteralNode.WithValue(method, 123);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, callNode.InputExecPins[0]);
        GraphUtil.ConnectExecPins(callNode.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
        GraphUtil.ConnectDataPins(badArgument.ValuePin, callNode.ArgumentPins[0]);
        vm.Class.Methods.Add(method);

        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(vm.Project!);
        await page.ErrorRow("CS1503").WaitVisibleAsync(Token, TimeSpan.FromSeconds(30));

        await page.ErrorRow("CS1503").DoubleClickAsync(Token);

        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(vm.OpenedGraph?.Graph == method), "the method with the error to open", Token);
        Assert.Contains(vm.OpenedGraph!.SelectedNodes, n => n.Node == callNode);

        // The viewport recentres on the revealed node (FR-034): still (0, 0), the reset every newly
        // opened graph starts at, would mean RevealNode's centering never ran.
        string? viewportX = await session.Graph.PropertyAsync(AutomationPropertyNames.ViewportX, Token);
        string? viewportY = await session.Graph.PropertyAsync(AutomationPropertyNames.ViewportY, Token);
        Assert.False(viewportX == "0" && viewportY == "0", "the viewport recentred on the revealed node");
    }
}
