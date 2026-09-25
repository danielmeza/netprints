using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>Pixel snapshots of key states against Snapshots/Baselines.</summary>
public class SnapshotTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SnapshotStore Store => UiArtifacts.Snapshots;

    private static async Task MatchWindowAsync(IUiDriver driver, UiElement window, string name, SnapshotOptions? options = null)
    {
        var element = await window.GetAsync(Token);
        Store.Match(name, await driver.ScreenshotAsync(element.Window, Token), options);
    }

    /// <summary>Masks an element (a caret or a focus ring that may blink), in window pixels.</summary>
    private static async Task<SnapshotMask> MaskOfAsync(UiElement element)
    {
        var bounds = (await element.GetAsync(Token)).Bounds;
        return new SnapshotMask((int)bounds.X - 2, (int)bounds.Y - 2, (int)bounds.Width + 4, (int)bounds.Height + 4);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task MainWindow()
    {
        using var sample = new SampleCopy();
        using var app = HeadlessApp.Start();
        await MatchWindowAsync(app.Driver, app.Main, "main-window-empty");

        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        await MatchWindowAsync(app.Driver, app.Main, "main-window-project");

        await app.Main.ShowProjectPaneAsync(Token);
        await MatchWindowAsync(app.Driver, app.Main, "main-window-project-pane");

        await app.Main.ShowSettingsPaneAsync(Token);
        await MatchWindowAsync(app.Driver, app.Main, "main-window-settings-pane");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClassEditorWithTheSampleGraph()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);

        await MatchWindowAsync(session.Driver, session.ClassEditor, "class-editor-main");
        Store.Match("node-call-method", await session.Graph.Node("CallMethodNode").ScreenshotAsync(Token)); // connected and unconnected pins
        Store.Match("inspector-method", await session.ClassEditor.InspectorColumn.ScreenshotAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task Inspectors()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var page = session.ClassEditor;

        await page.CreateVariableButton.ClickAsync(Token);
        await page.VariableNameText("Variable").ClickAsync(Token);
        await page.VariableInspector.WaitVisibleAsync(Token);
        Store.Match("inspector-variable", await page.InspectorColumn.ScreenshotAsync(Token));

        await page.ClassButton.ClickAsync(Token);
        await page.ClassInspector.WaitVisibleAsync(Token);
        await page.ClassInspector.GeneratedCode.WaitUntilAsync(e => (e.Text ?? "").Contains("class Program"), "generated code", Token);
        Store.Match("inspector-class", await page.InspectorColumn.ScreenshotAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryNodeKind()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.ClassEditor.CreateVariableButton.ClickAsync(Token);
        var variable = session.ClassVM.Variables.Single().Variable.Specifier;
        var graph = session.GraphVM;

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

        Store.Match("canvas-every-node-kind", await session.Graph.ScreenshotAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task PreviewCableSearchAndGetSet()
    {
        using var session = await EditorSession.OpenSampleMainAsync(Token);
        var graph = session.Graph;

        var from = await graph.Node("CallMethodNode").Output("Exec").Connector.CenterAsync(Token);
        var to = await graph.OffsetAsync(700, 450, Token);
        await session.Driver.PressAndMoveAsync(from, to, UiButton.Left, Token);
        Store.Match("canvas-preview-cable", await graph.ScreenshotAsync(Token));
        await session.Driver.ReleaseAsync(to, UiButton.Left, Token);
        var search = await graph.Search.WaitOpenAsync(Token);
        await session.Driver.PressAsync("Escape", Token); // close without choosing
        await search.WaitClosedAsync(Token);
        Assert.Equal(3, await graph.NodeCountAsync(Token));

        search = await (await graph.RightClickAtAsync(280, 392, Token)).WaitOpenAsync(Token);
        var mask = await MaskOfAsync(search.SearchBox);
        Store.Match("search-popup", await session.Driver.ScreenshotAsync((await graph.GetAsync(Token)).Window, Token),
            new SnapshotOptions { Masks = [mask] });
        await session.Driver.PressAsync("Escape", Token);
        await search.WaitClosedAsync(Token);

        var length = new VariableSpecifier("Length", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<string>(), VariableModifiers.None);
        session.GraphVM.GetSetChooser.Open(length, new GraphPoint(280, 392));
        await graph.GetSet.WaitOpenAsync(Token);
        Store.Match("get-set-chooser", await graph.GetSet.View.ScreenshotAsync(Token));
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
        using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);

        var references = await app.Main.OpenReferencesAsync(Token);
        await references.WaitForRowAsync("System.dll", Token);

        await MatchWindowAsync(app.Driver, references, "dialog-references");
    }
}
