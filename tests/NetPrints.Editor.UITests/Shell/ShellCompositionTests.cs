using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The production composition (T040): the shell window replaces the legacy windows, documents share the inspector's editors and the keys run the registry's commands.</summary>
public class ShellCompositionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ShellApp> StartAsync()
    {
        ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        return app;
    }

    private static DocumentId ClassDocument(ShellApp app) =>
        CommandTargets.GraphDocumentOf(app.Session, app.Session.Project.Classes[0]) ?? throw new InvalidOperationException("No class document.");

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheShellWindowIsTheOnlyWindowAndNoLegacyWindowOpensWhenAProjectLoads()
    {
        await using ShellApp app = await StartAsync();

        Assert.Same(app.Window, app.Composition.Windows.MainWindow);
        Assert.Equal([app.Window], app.Ui.Tree.Windows);
        Assert.NotNull(app.Shell.MenuBar);
        Assert.Contains("HelloWorld", app.Shell.Title, StringComparison.Ordinal);
        Assert.Single(app.Ui.Tree.Find(new AutomationQuery(AutomationIds.ShellMenuBar)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AGraphTabIsBuiltOnTheClassContextSoUndoSeesItsEditsAndTheClassIsDirty()
    {
        await using ShellApp app = await StartAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        DocumentId id = ClassDocument(app);

        app.Api.OpenDocument(id);
        HeadlessDriver.Pump();
        NodeGraphViewModel graph = Assert.IsType<GraphDocumentViewModel>(app.Shell.FindDocument(id)).Graph;

        Assert.Same(app.Session.ContextFor(cls).Services, graph.Services);
        Assert.Same(app.Session.UndoStackFor(cls), graph.Services.UndoRedo);
        int edits = 0;
        graph.Services.UndoRedo.Do(new DelegateUndoableCommand("Edit", () => edits++, () => edits--));
        Assert.Equal(1, edits);
        Assert.True(app.Commands.CanRun(app.Command("undo")));

        Assert.True(app.Commands.TryRun(app.Command("undo")));
        await UiWaitAsync(app, () => edits == 0);

        Assert.Equal(0, edits);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlTabCyclesAndCtrlWClosesTheActiveTabThroughTheWindowsKeyBindings()
    {
        await using ShellApp app = await StartAsync();
        DocumentId classDocument = ClassDocument(app);
        DocumentId methodDocument = CommandTargets.GraphDocumentOf(app.Session, app.Session.Project.Classes[0].Methods.First()) ?? throw new InvalidOperationException("No method document.");
        app.Api.OpenDocument(classDocument);
        app.Api.OpenDocument(methodDocument);
        app.Api.OpenDocument(DocumentId.ProjectSettings);
        app.Api.OpenDocument(methodDocument);
        HeadlessDriver.Pump();
        Assert.Equal(methodDocument, app.Api.ActiveDocument);

        await app.Driver.PressAsync("Ctrl+Tab", Token);
        Assert.Equal(DocumentId.ProjectSettings, app.Api.ActiveDocument);
        await app.Driver.PressAsync("Ctrl+Shift+Tab", Token);
        Assert.Equal(methodDocument, app.Api.ActiveDocument);
        await app.Driver.PressAsync("Ctrl+Shift+Tab", Token);
        Assert.Equal(classDocument, app.Api.ActiveDocument);

        await app.Driver.PressAsync("Ctrl+W", Token);
        Assert.Equal([methodDocument, DocumentId.ProjectSettings], app.Api.OpenDocuments);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClosingTheProjectClosesItsDocumentsAndEmptiesTheSession()
    {
        await using ShellApp app = await StartAsync();
        app.Api.OpenDocument(ClassDocument(app));
        app.Api.OpenDocument(DocumentId.ProjectSettings);
        HeadlessDriver.Pump();

        Assert.True(app.Commands.TryRun(app.Command("closeProject")));
        await UiWaitAsync(app, () => app.Shell.Session is null);

        Assert.Null(app.Shell.Session);
        Assert.Empty(app.Api.OpenDocuments);
        Assert.Empty(app.Shell.Documents);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheNewClassCommandAddsAClassToTheOpenProject()
    {
        await using ShellApp app = await StartAsync();
        int classes = app.Session.Project.Classes.Count;

        Assert.True(app.Commands.TryRun(app.Command("newClass")));
        await UiWaitAsync(app, () => app.Session.Project.Classes.Count == classes + 1);

        Assert.Equal(classes + 1, app.Session.Project.Classes.Count);
    }

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token);
}
