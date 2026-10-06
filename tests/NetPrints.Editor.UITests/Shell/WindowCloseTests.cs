using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Closing the shell window goes through the unsaved changes prompt (FR-022).</summary>
public class WindowCloseTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ShellApp> StartDirtyAsync()
    {
        ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();
        Assert.True(app.Session.Project.Classes[0].IsDirty);
        return app;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CancellingThePromptKeepsTheWindowOpenWithItsChanges()
    {
        await using ShellApp app = await StartDirtyAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        app.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;

        app.Window.Close();
        HeadlessDriver.Pump();

        Assert.Single(app.Dialogs.UnsavedCalls);
        Assert.True(app.Window.IsVisible);
        Assert.True(cls.IsDirty);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DontSaveClosesTheWindowAfterExactlyOnePrompt()
    {
        await using ShellApp app = await StartDirtyAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        app.Window.Close();
        HeadlessDriver.Pump();

        Assert.Single(app.Dialogs.UnsavedCalls);
        Assert.False(app.Window.IsVisible);
        Assert.True(cls.IsDirty);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SaveAllSavesThenClosesTheWindow()
    {
        await using ShellApp app = await StartDirtyAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        app.Dialogs.UnsavedAnswer = UnloadChoice.Save;

        app.Window.Close();
        HeadlessDriver.Pump();
        await UiWaitAsync(app, () => !app.Window.IsVisible);

        Assert.Single(app.Dialogs.UnsavedCalls);
        Assert.False(cls.IsDirty);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ACleanProjectClosesWithoutAPrompt()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);

        app.Window.Close();
        HeadlessDriver.Pump();

        Assert.Empty(app.Dialogs.UnsavedCalls);
        Assert.False(app.Window.IsVisible);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClosingATabNeverPrompts()
    {
        await using ShellApp app = await StartDirtyAsync();
        DocumentId id = CommandTargets.GraphDocumentOf(app.Session, app.Session.Project.Classes[0]) ?? throw new InvalidOperationException("No class document.");
        app.Api.OpenDocument(id);
        HeadlessDriver.Pump();

        app.Api.CloseDocument(id);
        HeadlessDriver.Pump();

        Assert.Empty(app.Dialogs.UnsavedCalls);
        Assert.Empty(app.Api.OpenDocuments);
    }

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token);
}
