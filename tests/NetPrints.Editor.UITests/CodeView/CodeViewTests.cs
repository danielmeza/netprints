using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
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
        session.Window.GetVisualDescendants().OfType<NetPrints.Editor.CodeView.CodeView>().Single(view => view.FindAncestorOfType<NetPrints.Editor.Inspectors.ClassInspectorView>() is not null);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CodeViewIsHighlightedNumberedFoldedAndDoesNotWrap()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Program", StringComparison.Ordinal), "generated code", Token);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        Assert.True(codeView.CodeEditor.ShowLineNumbers);
        Assert.False(codeView.CodeEditor.WordWrap);
        var viewModel = codeView.ViewModel ?? throw new InvalidOperationException("The code view has no view model.");
        Assert.NotEmpty(viewModel.Foldings); // at least the class and the Main method

        // ED-T01's other half (highlighted tokens) is verified visually: reviewed on regeneration.
        Store.Match("class-inspector-code-view", await inspector.ScreenshotAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DetachingAndReattachingKeepsFoldingAndHoverWorking()
    {
        // R2-13: CodeView used to dispose its folding manager and TextMate installation for good on
        // the first detach; a later re-attach (re-templating, moving the control into a tab or dock)
        // came back as plain text with no hover. It now installs/uninstalls symmetrically instead.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("WriteLine", StringComparison.Ordinal), "generated code", Token);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        var viewModel = codeView.ViewModel ?? throw new InvalidOperationException("The code view has no view model.");
        Assert.NotEmpty(viewModel.Foldings);

        Panel parent = codeView.Parent as Panel ?? throw new InvalidOperationException("CodeView's parent is not a Panel.");
        int index = parent.Children.IndexOf(codeView);
        parent.Children.RemoveAt(index); // detaches
        parent.Children.Insert(index, codeView); // re-attaches to the same visual tree

        // Folding survives the round trip: RefreshFoldings re-applies the VM's current state on attach.
        Assert.NotEmpty(viewModel.Foldings);

        string code = viewModel.Code;
        // "WriteLine(", not "WriteLine": the generated code also has a "// Console.WriteLine" comment above the call.
        int offset = code.IndexOf("WriteLine(", StringComparison.Ordinal) + 2;
        await viewModel.ShowQuickInfoCommand.ExecuteAsync(offset);

        bool isOpen = await inspector.ClassCodeView.GetAsync<bool>(AutomationPropertyNames.ToolTipIsOpen, Token);
        Assert.True(isOpen, "the tooltip is open after re-attaching");
        string? tip = await inspector.ClassCodeView.PropertyAsync(AutomationPropertyNames.ToolTip, Token);
        Assert.Contains("WriteLine", tip ?? "", StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringWriteLineShowsSignatureAndSummary()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("WriteLine", StringComparison.Ordinal), "generated code", Token);

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        var viewModel = codeView.ViewModel ?? throw new InvalidOperationException("The code view has no view model.");
        string code = viewModel.Code;
        // "WriteLine(", not "WriteLine": the generated code also has a "// Console.WriteLine" comment above the call.
        int offset = code.IndexOf("WriteLine(", StringComparison.Ordinal) + 2;

        await viewModel.ShowQuickInfoCommand.ExecuteAsync(offset);

        // OWN-01 (owner report): Avalonia only auto-opens a tooltip on its own pointer-enter, never
        // when the tip is merely set programmatically, so asserting the tip's text alone (as this test
        // used to) does not catch a tooltip that is set but never shown.
        bool isOpen = await inspector.ClassCodeView.GetAsync<bool>(AutomationPropertyNames.ToolTipIsOpen, Token);
        Assert.True(isOpen, "the tooltip is open");
        string? tip = await inspector.ClassCodeView.PropertyAsync(AutomationPropertyNames.ToolTip, Token);
        Assert.Contains("WriteLine", tip ?? "", StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task HoveringADiagnosticSpanShowsItAboveTheQuickInfo()
    {
        // OWN-02 (owner report): hovering a squiggle shows the diagnostic(s) under the cursor, above
        // the symbol quick info. Same CS1503 setup as
        // DoubleClickingADiagnosticRowOpensTheGraphSelectsAndRevealsTheNode.
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var cls = session.Class;
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

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

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(cls.Project ?? throw new InvalidOperationException("No project is open."));

        NetPrints.Editor.CodeView.CodeView codeView = FindCodeView(session);
        var viewModel = codeView.ViewModel ?? throw new InvalidOperationException("The code view has no view model.");
        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(viewModel.Diagnostics.Any(d => d.Id == "CS1503")),
            "the CS1503 diagnostic to reach the code view", Token, TimeSpan.FromSeconds(30));

        CodeDiagnostic diagnostic = viewModel.Diagnostics.First(d => d.Id == "CS1503");
        LinePositionSpan span = diagnostic.Span ?? throw new InvalidOperationException("The CS1503 diagnostic has no Span.");
        int offset = SourceText.From(viewModel.Code).Lines.GetPosition(span.Start);

        await viewModel.ShowQuickInfoCommand.ExecuteAsync(offset);

        bool isOpen = await inspector.ClassCodeView.GetAsync<bool>(AutomationPropertyNames.ToolTipIsOpen, Token);
        Assert.True(isOpen, "the tooltip is open");
        string? tip = await inspector.ClassCodeView.PropertyAsync(AutomationPropertyNames.ToolTip, Token);
        Assert.Contains("CS1503", tip ?? "", StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingADiagnosticRowOpensTheGraphSelectsAndRevealsTheNode()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var cls = session.Class;

        // A second method with a real CS1503 (Guid.Parse(string) fed an int), wired into its flow
        // (same technique as SourceMapTests/CodeAnalysisHostTests): the graph model does not itself
        // enforce pin type compatibility.
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

        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(cls.Project ?? throw new InvalidOperationException("No project is open."));
        await session.Page.Bottom.ShowAsync(PanelContributions.ErrorsId, Token);
        await session.Page.Bottom.ErrorRow().WaitVisibleAsync(Token, TimeSpan.FromSeconds(30));

        await session.Page.Bottom.ErrorRow().DoubleClickAsync(Token);

        await UiWait.UntilAsync(session.Driver, () => Task.FromResult(session.App.Shell.ActiveDocument is GraphDocumentViewModel { Graph: { } shown } && shown.Graph == method),
            "the method with the error to open", Token);
        Assert.Contains(session.GraphViewModel.SelectedNodes, n => n.Node == callNode);

        // The viewport recentres on the revealed node (FR-034): still (0, 0), the reset every newly
        // opened graph starts at, would mean RevealNode's centering never ran.
        string? viewportX = await session.Graph.PropertyAsync(AutomationPropertyNames.ViewportX, Token);
        string? viewportY = await session.Graph.PropertyAsync(AutomationPropertyNames.ViewportY, Token);
        Assert.False(viewportX == "0" && viewportY == "0", "the viewport recentred on the revealed node");
    }
}
