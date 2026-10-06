using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Opening another project in the window of a project with unsaved changes (FR-018, FR-022).</summary>
public class UnloadWhileDirtyTests
{
    private const string OpenPickerTitle = "Open Project";
    private static readonly TimeSpan BackupDelay = TimeSpan.FromMilliseconds(50);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpenProjectAsksFirstCancelKeepsTheProjectAndDontSaveReplacesIt()
    {
        await using ShellApp app = ShellApp.Start();
        using var other = new SampleCopy();
        await app.OpenSampleAsync(Token);
        string first = app.Session.Project.Path;
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();

        app.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;
        app.FilePicker.Enqueue("open", OpenPickerTitle, other.ProjectPath);
        Assert.True(app.Commands.TryRun(app.Command("openProject")));
        await UiWaitAsync(app, () => app.Dialogs.UnsavedCalls.Count == 1);
        HeadlessDriver.Pump();

        Assert.Equal(first, app.Session.Project.Path);
        Assert.Empty(app.FilePicker.Requests);
        Assert.True(app.Session.Project.Classes[0].IsDirty);

        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;
        Assert.True(app.Commands.TryRun(app.Command("openProject")));
        await UiWaitAsync(app, () => app.Shell.Session?.Project.Path == other.ProjectPath);

        Assert.Equal(2, app.Dialogs.UnsavedCalls.Count);
        Assert.Equal(other.ProjectPath, app.Session.Project.Path);
        Assert.False(app.Session.Unsaved.HasUnsavedFiles);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NewProjectAsksFirstCancelShowsNoPickerAndDontSaveCreatesTheProject()
    {
        await using ShellApp app = ShellApp.Start();
        using var target = new TempFolder();
        await app.OpenSampleAsync(Token);
        string first = app.Session.Project.Path;
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();

        app.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;
        app.Dialogs.ShowNewProject = async dialog =>
        {
            dialog.Name = "Fresh";
            dialog.Folder = Path.Combine(target.Path, "Fresh");
            await dialog.CreateCommand.ExecuteAsync(null);
            return dialog.Result;
        };
        Assert.True(app.Commands.TryRun(app.Command("newProject")));
        await UiWaitAsync(app, () => app.Dialogs.UnsavedCalls.Count == 1);
        HeadlessDriver.Pump();

        Assert.Equal(first, app.Session.Project.Path);
        Assert.Empty(app.FilePicker.Requests);

        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;
        Assert.True(app.Commands.TryRun(app.Command("newProject")));
        await UiWaitAsync(app, () => app.Shell.Session?.Project.Path != first, TimeSpan.FromSeconds(60));

        Assert.Equal("Fresh", app.Session.Project.Name);
        Assert.False(app.Session.Unsaved.HasUnsavedFiles);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ExitAsksOnceThenClosesTheWindow()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();
        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(app.Commands.TryRun(app.Command("exit")));
        await UiWaitAsync(app, () => !app.Window.IsVisible);

        Assert.Single(app.Dialogs.UnsavedCalls);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DontSaveAtWindowCloseDeletesTheBackups()
    {
        using var data = new TempFolder();
        await using ShellApp app = ShellApp.Start(Backups(data));
        await app.OpenSampleAsync(Token);
        BackupStore store = await BackUpAnEditAsync(app, data);
        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        app.Window.Close();
        HeadlessDriver.Pump();
        await UiWaitAsync(app, () => !app.Window.IsVisible);

        await UiWaitAsync(app, () => store.List().Count == 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DontSaveThenACancelledPickerKeepsTheBackups()
    {
        using var data = new TempFolder();
        await using ShellApp app = ShellApp.Start(Backups(data));
        await app.OpenSampleAsync(Token);
        BackupStore store = await BackUpAnEditAsync(app, data);
        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;

        Assert.True(app.Commands.TryRun(app.Command("openProject")));
        await UiWaitAsync(app, () => app.FilePicker.Requests.Count == 1);
        HeadlessDriver.Pump();

        Assert.True(app.Session.Project.Classes[0].IsDirty);
        Assert.NotEmpty(store.List());
    }

    private static BackupOptions Backups(TempFolder data) =>
        new(new EditorDataPaths(data.Path), new RealEditorFileSystem(), TimeProvider.System, BackupDelay);

    private static async Task<BackupStore> BackUpAnEditAsync(ShellApp app, TempFolder data)
    {
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();
        var store = new BackupStore(new EditorDataPaths(data.Path), new RealEditorFileSystem(), app.Session.Project.Path, NullLogger.Instance);
        await UiWaitAsync(app, () => store.List().Count == 1);
        return store;
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp folder is harmless.
            }
        }
    }

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition, TimeSpan timeout) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token, timeout);

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token);
}
