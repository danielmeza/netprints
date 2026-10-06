using Avalonia.Headless.XUnit;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Opening another project in the window of a project with unsaved changes (FR-018, FR-022).</summary>
public class UnloadWhileDirtyTests
{
    private const string OpenPickerTitle = "Open Project";

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

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token);
}
