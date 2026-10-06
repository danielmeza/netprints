using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using Nodify.Avalonia;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>A project's open documents, active tab and viewports are saved when it unloads and restored when it opens again (US6, FR-050, FR-051, SC-005).</summary>
public class RestoreSessionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DocumentId IdOf(ShellApp app, object graph) =>
        CommandTargets.GraphDocumentOf(app.Session, graph) ?? throw new InvalidOperationException("The graph has no document.");

    private static async Task WaitAsync(ShellApp app, Func<bool> condition) =>
        await Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token, TimeSpan.FromSeconds(60));

    private static NodifyEditor Editor(ShellApp app) => app.Window.GetVisualDescendants().OfType<NodifyEditor>().Single();

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReopeningAProjectBringsBackItsDocumentsInOrderTheActiveTabAndTheViewports()
    {
        var store = new MemoryStateStore();
        await using ShellApp app = ShellApp.Start(store);
        await app.OpenSampleAsync(Token);
        ClassGraph cls = app.Session.Project.Classes.Single(c => c.FullName == EditorSession.ClassName);
        DocumentId classId = IdOf(app, cls);
        DocumentId mainId = IdOf(app, cls.Methods.Single(m => m.Name == "Main"));
        app.Api.OpenDocument(mainId);
        app.Api.OpenDocument(classId);
        HeadlessDriver.Pump();
        var main = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(mainId));
        var classDocument = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(classId));
        main.ViewportLocation = new GraphPoint(30, 40);
        main.ViewportZoom = 0.5;
        classDocument.ViewportLocation = new GraphPoint(-5, 6);
        classDocument.ViewportZoom = 0.8;
        app.Api.ActivateDocument(classId);
        HeadlessDriver.Pump();

        Assert.True(app.Commands.TryRun(app.Command("closeProject")));
        await WaitAsync(app, () => app.Shell.Session is null);

        Assert.Empty(app.Api.OpenDocuments);
        SessionState saved = store.Sessions[app.ProjectPath];
        Assert.Equal([mainId.ToString(), classId.ToString()], saved.OpenDocuments);
        Assert.Equal(classId.ToString(), saved.ActiveDocument);
        Assert.Equal(new ViewportState(30, 40, 0.5), saved.Viewports[mainId.ToString()]);

        await app.Composition.StartAsync([app.ProjectPath]);
        await WaitAsync(app, () => app.Shell.Session is not null);
        HeadlessDriver.Pump();

        Assert.Equal([mainId, classId], app.Api.OpenDocuments);
        Assert.Equal(classId, app.Api.ActiveDocument);
        var restoredMain = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(mainId));
        Assert.NotSame(main, restoredMain);
        Assert.Equal(new GraphPoint(30, 40), restoredMain.ViewportLocation);
        Assert.Equal(0.5, restoredMain.ViewportZoom);
        var restoredClass = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(classId));
        Assert.Equal(new GraphPoint(-5, 6), restoredClass.ViewportLocation);
        Assert.Equal(0.8, restoredClass.ViewportZoom);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheCanvasShowsTheRestoredViewportAndPansAndZoomsAreWrittenBackToTheDocument()
    {
        var store = new MemoryStateStore();
        await using ShellApp app = ShellApp.Start(store);
        await app.OpenSampleAsync(Token);
        ClassGraph cls = app.Session.Project.Classes.Single(c => c.FullName == EditorSession.ClassName);
        DocumentId mainId = IdOf(app, cls.Methods.Single(m => m.Name == "Main"));
        app.Api.OpenDocument(mainId);
        HeadlessDriver.Pump();
        var main = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(mainId));

        main.ViewportLocation = new GraphPoint(120, 80);
        main.ViewportZoom = 0.6;
        HeadlessDriver.Pump();
        NodifyEditor editor = Editor(app);
        Assert.Equal(new Point(120, 80), editor.ViewportLocation);
        Assert.Equal(0.6, editor.ViewportZoom, 3);

        editor.ViewportLocation = new Point(10, 20);
        editor.ViewportZoom = 0.4;
        HeadlessDriver.Pump();
        Assert.Equal(new GraphPoint(10, 20), main.ViewportLocation);
        Assert.Equal(0.4, main.ViewportZoom, 3);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AGraphThatNoLongerExistsIsSkippedWhenTheProjectOpens()
    {
        var store = new MemoryStateStore();
        await using ShellApp app = ShellApp.Start(store);
        using var other = new SampleCopy();
        string stale = DocumentId.Graph("Gone.netpc.json", "method:m000000000000000").ToString();
        await app.OpenSampleAsync(Token);
        ClassGraph cls = app.Session.Project.Classes.Single(c => c.FullName == EditorSession.ClassName);
        DocumentId mainId = IdOf(app, cls.Methods.Single(m => m.Name == "Main"));
        store.SaveSession(new SessionState(StateFile.CurrentVersion, other.ProjectPath, [stale, mainId.ToString()], stale, new Dictionary<string, ViewportState>()));

        app.FilePicker.Enqueue("open", "Open Project", other.ProjectPath);
        Assert.True(app.Commands.TryRun(app.Command("openProject")));
        await WaitAsync(app, () => app.Shell.Session?.Project.Path == other.ProjectPath);
        HeadlessDriver.Pump();

        Assert.Equal([mainId], app.Api.OpenDocuments);
        Assert.Equal(mainId, app.Api.ActiveDocument);
    }
}
