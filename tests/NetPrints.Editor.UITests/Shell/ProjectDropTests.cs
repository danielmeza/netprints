using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Dropping a project on the main window (FR-049): a .csproj or a folder opens after the unsaved changes prompt; anything else shows the reason.</summary>
public class ProjectDropTests
{
    private static readonly Point Target = new(400, 300);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The dropped files and folders as the platform's storage provider hands them over.</summary>
    private static async Task<DataTransfer> FilesOfAsync(ShellApp app, params string[] paths)
    {
        IStorageProvider storage = app.Window.StorageProvider;
        var data = new DataTransfer();
        foreach (string path in paths)
        {
            IStorageItem? item = Directory.Exists(path)
                ? await storage.TryGetFolderFromPathAsync(new Uri(path))
                : await storage.TryGetFileFromPathAsync(new Uri(path));
            data.Add(DataTransferItem.Create(DataFormat.File, Assert.IsAssignableFrom<IStorageItem>(item)));
        }

        return data;
    }

    private static void Drop(ShellApp app, DataTransfer data)
    {
        app.Window.DragDrop(Target, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
        app.Window.DragDrop(Target, RawDragEventType.Drop, data, DragDropEffects.Copy);
        HeadlessDriver.Pump();
    }

    private static Task UiWaitAsync(ShellApp app, Func<bool> condition) =>
        Testing.Ui.Driving.UiWait.UntilAsync(app.Driver, () => Task.FromResult(condition()), "condition", Token, TimeSpan.FromSeconds(60));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADroppedCsprojOpensWhileNoProjectIsOpen()
    {
        await using ShellApp app = ShellApp.Start();

        Drop(app, await FilesOfAsync(app, app.ProjectPath));
        await UiWaitAsync(app, () => app.Shell.Session is not null);

        Assert.Equal(app.ProjectPath, app.Session.Project.Path);
        Assert.Null(app.Shell.StartPageError);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADroppedFolderWithOneProjectOpensIt()
    {
        await using ShellApp app = ShellApp.Start();

        Drop(app, await FilesOfAsync(app, app.Sample.Directory));
        await UiWaitAsync(app, () => app.Shell.Session is not null);

        Assert.Equal(app.ProjectPath, app.Session.Project.Path);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADropOverAProjectWithUnsavedChangesAsksFirstAndCancelKeepsIt()
    {
        await using ShellApp app = ShellApp.Start();
        using var other = new SampleCopy();
        await app.OpenSampleAsync(Token);
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();

        app.Dialogs.UnsavedAnswer = UnloadChoice.Cancel;
        Drop(app, await FilesOfAsync(app, other.ProjectPath));
        await UiWaitAsync(app, () => app.Dialogs.UnsavedCalls.Count == 1);

        Assert.Equal(app.ProjectPath, app.Session.Project.Path);

        app.Dialogs.UnsavedAnswer = UnloadChoice.Discard;
        Drop(app, await FilesOfAsync(app, other.ProjectPath));
        await UiWaitAsync(app, () => app.Shell.Session?.Project.Path == other.ProjectPath);

        Assert.Equal(2, app.Dialogs.UnsavedCalls.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADropThatIsNoProjectShowsTheErrorOnTheStartPageAndAsksNothing()
    {
        await using ShellApp app = ShellApp.Start();
        string notes = Path.Combine(app.Sample.Directory, "notes.txt");
        await File.WriteAllTextAsync(notes, "notes", Token);

        Drop(app, await FilesOfAsync(app, notes));
        await UiWaitAsync(app, () => app.Shell.StartPageError is not null);

        Assert.Contains("notes.txt", app.Shell.StartPageError, StringComparison.Ordinal);
        Assert.Null(app.Shell.Session);
        Assert.Empty(app.Dialogs.UnsavedCalls);
        Assert.Empty(app.Dialogs.Errors);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ADropThatIsNoProjectWhileAProjectIsOpenShowsAStatusMessageAndKeepsTheProject()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        app.Session.ContextFor(app.Session.Project.Classes[0]).CreateVariable();
        string notes = Path.Combine(app.Sample.Directory, "notes.txt");
        await File.WriteAllTextAsync(notes, "notes", Token);

        Drop(app, await FilesOfAsync(app, notes));
        await UiWaitAsync(app, () => app.Shell.StatusMessage?.Contains("notes.txt", StringComparison.Ordinal) == true);

        Assert.Equal(app.ProjectPath, app.Session.Project.Path);
        Assert.Null(app.Shell.StartPageError);
        Assert.Empty(app.Dialogs.UnsavedCalls);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SeveralDroppedItemsOpenNothing()
    {
        await using ShellApp app = ShellApp.Start();
        using var other = new SampleCopy();

        Drop(app, await FilesOfAsync(app, app.ProjectPath, other.ProjectPath));
        await UiWaitAsync(app, () => app.Shell.StartPageError is not null);

        Assert.Null(app.Shell.Session);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DroppedTextIsIgnored()
    {
        await using ShellApp app = ShellApp.Start();
        var text = new DataTransfer();
        text.Add(DataTransferItem.CreateText("hello"));

        Drop(app, text);

        Assert.Null(app.Shell.Session);
        Assert.Null(app.Shell.StartPageError);
    }
}
