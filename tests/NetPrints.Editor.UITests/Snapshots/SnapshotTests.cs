using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.References;
using NetPrints.Testing.Ui.Shell;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>
/// Pixel snapshots of the key states, compared with the committed baselines in
/// Snapshots/Baselines (tolerant: per-pixel threshold, maximum differing share, masks).
/// Regenerate with NETPRINTS_UPDATE_SNAPSHOTS=1; new baselines are reviewed before acceptance.
/// </summary>
public class SnapshotTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SnapshotStore Store => UiArtifacts.Snapshots;

    private static Task MatchStableAsync(string name, Func<CancellationToken, Task<UiImage>> capture, SnapshotOptions? options = null) =>
        Store.MatchStableAsync(name, capture, options, cancellationToken: Token);

    private static Task MatchWindowAsync(IUiDriver driver, UiElement window, string name, SnapshotOptions? options = null) =>
        MatchStableAsync(name, async cancellationToken =>
        {
            var element = await window.GetAsync(cancellationToken);
            return await driver.ScreenshotAsync(element.Window, cancellationToken);
        }, options);

    /// <summary>Masks an element (a caret or a focus ring that may blink), in window pixels.</summary>
    private static async Task<SnapshotMask> MaskOfAsync(UiElement element)
    {
        var bounds = (await element.GetAsync(Token)).Bounds;
        return new SnapshotMask((int)bounds.X - 2, (int)bounds.Y - 2, (int)bounds.Width + 4, (int)bounds.Height + 4);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClassEditorWithTheSampleGraph()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);

        await MatchWindowAsync(session.Driver, session.Page, "class-editor-main");
        await MatchStableAsync("node-call-method", session.Graph.Node("CallMethodNode").ScreenshotAsync); // connected and unconnected pins
        await MatchStableAsync("inspector-method", session.Page.Inspector.ScreenshotAsync);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task Inspectors()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var tree = session.Page.Tree;
        var inspector = session.Page.Inspector;

        await session.AddVariableAsync(Token);
        await tree.SelectAsync(await tree.RevealAsync(tree.Variable("Variable"), ProjectTreePage.VariablesGroup, Token), Token);
        await inspector.VariableInspector.WaitVisibleAsync(Token);
        await MatchStableAsync("inspector-variable", inspector.ScreenshotAsync);

        await tree.SelectAsync(tree.Class("Program"), Token);
        await inspector.ClassInspector.WaitVisibleAsync(Token);
        await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Program"), "generated code", Token);
        await MatchStableAsync("inspector-class", inspector.ScreenshotAsync);
    }

    /// <summary>
    /// Reproduction of issue #13: opens the class inspector <c>NETPRINTS_SNAPSHOT_REPEAT</c> times (default 20),
    /// compares the first frame after the code text appears with the baseline, then compares the settled frame.
    /// Explicit: run it alone, ideally pinned to few CPUs. Fails if any settled frame mismatches; the count of
    /// immediate mismatches is the flake rate of a single capture.
    /// </summary>
    [AvaloniaFact(Explicit = true, Timeout = 900_000)]
    public async Task ClassInspectorSnapshotRepeatedly()
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("NETPRINTS_SNAPSHOT_REPEAT"), out int parsed) ? parsed : 20;
        int immediate = 0, settled = 0;
        for (int i = 0; i < runs; i++)
        {
            await using var session = await EditorSession.OpenSampleMainAsync(Token);
            var inspector = session.Page.Inspector;
            await session.AddVariableAsync(Token);
            await session.Page.Tree.SelectAsync(session.Page.Tree.Class("Program"), Token);
            await inspector.ClassInspector.WaitVisibleAsync(Token);
            await inspector.ClassCodeView.WaitUntilAsync(e => (e.Text ?? "").Contains("class Program", StringComparison.Ordinal), "generated code", Token);

            var first = await inspector.ScreenshotAsync(Token);
            var baseline = new UiImage(File.ReadAllBytes(Path.Combine(Store.BaselineDirectory, "inspector-class.png")));
            if (!SnapshotComparer.Compare(first, baseline, SnapshotOptions.Default).Matches)
            {
                immediate++;
            }

            try
            {
                await MatchStableAsync("inspector-class", inspector.ScreenshotAsync);
            }
            catch (SnapshotMismatchException)
            {
                settled++;
            }
        }

        TestContext.Current.SendDiagnosticMessage($"inspector-class over {runs} runs: {immediate} immediate captures mismatched, {settled} settled captures mismatched");
        File.WriteAllText(Path.Combine(Store.OutputDirectory, "repeat.txt"), $"{runs} runs: immediate mismatches {immediate}, settled mismatches {settled}{Environment.NewLine}");
        Assert.Equal(0, settled);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MethodEntryWithParameters()
    {
        // OWN-05b baseline: three parameters of different name lengths and types, to visually confirm
        // each parameter's type pin and value pin land on the same row (PinAlignmentTests has the pixel
        // checks; this is the human-reviewable picture of the fix).
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var entryNode = ((MethodGraph)session.GraphViewModel.Graph).MethodEntryNode;
        entryNode.AddArgument();
        entryNode.AddArgument();
        entryNode.AddArgument();
        entryNode.OutputDataPins[0].Name = "x";
        entryNode.OutputDataPins[0].PinType.Value = TypeSpecifier.FromType<int>();
        entryNode.OutputDataPins[1].Name = "someValue";
        entryNode.OutputDataPins[1].PinType.Value = TypeSpecifier.FromType<string>();
        entryNode.OutputDataPins[2].Name = "aVeryLongParameterName";
        entryNode.OutputDataPins[2].PinType.Value = TypeSpecifier.FromType<bool>();
        entryNode.PositionX = 28; // the extra-wide node (from the long name above) would otherwise
        entryNode.PositionY = 300; // overlap Console.WriteLine at its usual sample position.
        await session.WaitForRenderedAsync(Token);
        HeadlessDriver.Pump(); // the renames above don't add/remove nodes, so WaitForRenderedAsync's
                               // node/cable count check is already satisfied; pump once more so the
                               // node's width settles to the new (longer) pin names before the crop.

        await MatchStableAsync("node-method-entry-parameters", session.Graph.Node("MethodEntryNode").ScreenshotAsync);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryNodeKind()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.AddVariableAsync(Token);
        var variable = session.ClassContext.Variables.Single().Variable.Specifier;
        var graph = session.GraphViewModel;

        // Arrange through the API: one node of each kind, on a grid below the sample's nodes.
        var add = new List<Func<GraphPoint, Node>>
        {
            p => graph.AddNode<IfElseNode>(p),
            p => graph.AddNode<ForLoopNode>(p),
            p => graph.AddNode<LiteralNode>(p, null, TypeSpecifier.FromType<int>()),
            p => graph.AddNode<TernaryNode>(p),
            p => graph.AddNode<ThrowNode>(p),
            p => graph.AddNode<MakeArrayNode>(p),
            p => graph.AddNode<TypeNode>(p, null, TypeSpecifier.FromType<string>()),
            p => graph.AddNode<ExplicitCastNode>(p),
            p => graph.AddNode<VariableGetterNode>(p, null, variable),
            p => graph.AddNode<VariableSetterNode>(p, null, variable),
            p => graph.AddNode<DefaultNode>(p),
            p => graph.AddNode<AwaitNode>(p),
        };
        for (int i = 0; i < add.Count; i++)
        {
            add[i](new GraphPoint(28 + i % 4 * 336, 280 + i / 4 * 224));
        }

        await session.WaitForRenderedAsync(Token);
        for (int i = 0; i < 3; i++)
        {
            await session.Graph.WheelAsync(await session.Graph.OffsetAsync(0, 0, Token), -1, Token); // zoom out around the origin
        }

        await MatchStableAsync("canvas-every-node-kind", session.Graph.ScreenshotAsync);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PreviewCableSearchAndGetSet()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;

        var from = await graph.Node("CallMethodNode").Output("Exec").Connector.CenterAsync(Token);
        var to = await graph.OffsetAsync(700, 450, Token);
        await session.Driver.PressAndMoveAsync(from, to, UiButton.Left, Token);
        await MatchStableAsync("canvas-preview-cable", graph.ScreenshotAsync);
        await session.Driver.ReleaseAsync(to, UiButton.Left, Token);
        var search = await graph.Search.WaitOpenAsync(Token);
        await session.Driver.PressAsync("Escape", Token); // close without choosing
        await search.WaitClosedAsync(Token);
        Assert.Equal(3, await graph.NodeCountAsync(Token));

        search = await (await graph.RightClickAtAsync(280, 392, Token)).WaitOpenAsync(Token);
        var mask = await MaskOfAsync(search.SearchBox);
        await MatchStableAsync("search-popup", async cancellationToken => await session.Driver.ScreenshotAsync((await graph.GetAsync(cancellationToken)).Window, cancellationToken),
            new SnapshotOptions { Masks = [mask] });
        await session.Driver.PressAsync("Escape", Token);
        await search.WaitClosedAsync(Token);

        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        session.GraphViewModel.GetSetChooser.Open(length, new GraphPoint(280, 392));
        await graph.GetSet.WaitOpenAsync(Token);
        await MatchStableAsync("get-set-chooser", graph.GetSet.View.ScreenshotAsync);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task Dialogs()
    {
        using var ui = HeadlessUi.Create();
        ui.Show(new ErrorDialog("Failed to run project", "System.InvalidOperationException: boom\n   at Somewhere()"));
        await MatchWindowAsync(ui.Driver, new ErrorDialogPage(ui.Driver), "dialog-error");
        ui.Tree.Windows.Last().Close();

        ui.Show(new SelectTypeDialog([TypeSpecifier.FromType<object>(), TypeSpecifier.FromType<string>()], TypeSpecifier.FromType<object>()));
        await MatchWindowAsync(ui.Driver, new SelectTypeDialogPage(ui.Driver), "dialog-select-type");
        ui.Tree.Windows.Last().Close();

        var stringType = TypeSpecifier.FromType<string>();
        ui.Show(new SelectMethodDialog([new MethodSpecifier("Trim", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, [])]));
        await MatchWindowAsync(ui.Driver, new SelectMethodDialogPage(ui.Driver), "dialog-select-method");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReferencesDialog()
    {
        using var sample = new SampleCopy();
        await using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);

        await (app.Composition.ProjectActions ?? throw new InvalidOperationException("No shell.")).ShowReferencesAsync(Token);
        var references = new ReferencesDialogPage(app.Driver);
        await references.GetAsync(Token);
        // The SDK-style sample declares no explicit references (research.md R21): wait for the
        // dialog itself to settle instead of a specific row.
        await references.AddAssemblyButton.GetAsync(Token);

        await MatchWindowAsync(app.Driver, references, "dialog-references");
    }
}
