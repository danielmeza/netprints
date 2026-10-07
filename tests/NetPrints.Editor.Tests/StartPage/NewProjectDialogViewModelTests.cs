using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>New project with a location and a name (FR-048): the project folder is location/name, the preview shows it, a folder that is not empty is rejected, and the last location is remembered.</summary>
public sealed class NewProjectDialogViewModelTests : IDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly InMemoryEditorFileSystem fileSystem = new();
    private readonly FakeTimeProvider time = new();

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string TempRoot()
    {
        string root = TestPaths.CreateTempDirectory();
        cleanup.Add(root);
        return root;
    }

    private JsonEditorStateStore Store() => new(new EditorDataPaths("/state-root"), fileSystem, NullLogger<JsonEditorStateStore>.Instance);

    private NewProjectDialogViewModel Dialog(ProjectLocations locations)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        var service = new ProjectTemplateService(() => registry.ProjectTemplates, id => id == DefaultProjectProfile.ProfileId ? DefaultProjectProfile.Instance : null, editor.Projects);
        return new NewProjectDialogViewModel(service, editor.FilePicker, locations, time);
    }

    private async Task SettleAsync(NewProjectDialogViewModel dialog)
    {
        time.Advance(NewProjectDialogViewModel.ValidationDelay);
        await dialog.ValidationTask;
    }

    [Fact]
    public void TheLocationStartsAtTheDefaultUnderTheDocumentsFolder()
    {
        string documents = TempRoot();

        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), documents));

        Assert.Equal(Path.Combine(documents, "NetPrints"), dialog.Location);
        Assert.Equal("", dialog.Folder);
    }

    [Fact]
    public async Task TheProjectFolderIsTheLocationAndTheNameAndTheSecondLineOfTheDialogShowsIt()
    {
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        var changed = new List<string?>();
        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        string location = TempRoot();

        dialog.Location = location;
        dialog.Name = "Demo";
        await SettleAsync(dialog);

        Assert.Equal(Path.Combine(location, "Demo"), dialog.Folder);
        Assert.Contains(nameof(NewProjectDialogViewModel.Folder), changed);
        Assert.Null(dialog.Message);
        Assert.True(dialog.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFolderThatExistsAndIsNotEmptyIsRejectedAndAnEmptyOneIsAccepted()
    {
        string location = TempRoot();
        Directory.CreateDirectory(Path.Combine(location, "Taken"));
        File.WriteAllText(Path.Combine(location, "Taken", "notes.txt"), "mine");
        Directory.CreateDirectory(Path.Combine(location, "Empty"));
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Location = location;

        dialog.Name = "Taken";
        await SettleAsync(dialog);
        Assert.Contains("not empty", dialog.Message, StringComparison.Ordinal);
        Assert.False(dialog.CreateCommand.CanExecute(null));

        dialog.Name = "Empty";
        await SettleAsync(dialog);
        Assert.Null(dialog.Message);
        Assert.True(dialog.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreatingRemembersTheLocationForTheNextDialog()
    {
        JsonEditorStateStore store = Store();
        string documents = TempRoot();
        string location = TempRoot();
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(store, documents));
        dialog.Location = location;
        dialog.Name = "Remembered";
        await SettleAsync(dialog);

        await dialog.CreateCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(location, "Remembered", "Remembered.csproj"), dialog.Result);
        Assert.Equal(location, new ProjectLocations(Store(), documents).Last);
        Assert.Equal(location, Dialog(new ProjectLocations(Store(), documents)).Location);
    }

    [Fact]
    public async Task ACancelledOrFailedDialogRemembersNothing()
    {
        JsonEditorStateStore store = Store();
        string documents = TempRoot();
        editor.Projects.FailApply = _ => new IOException("disk full");
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(store, documents));
        dialog.SelectedTemplate = dialog.Templates.Single(template => template.Id == "netprints.template.library");
        dialog.Location = TempRoot();
        dialog.Name = "Broken";
        await SettleAsync(dialog);

        await dialog.CreateCommand.ExecuteAsync(null);
        dialog.CancelCommand.Execute(null);

        Assert.Null(store.LoadStart()?.NewProjectLocation);
    }

    [Fact]
    public async Task BrowseChangesTheLocationAndKeepsTheName()
    {
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Name = "Keep";
        string picked = TempRoot();
        editor.FilePicker.FolderAnswers.Enqueue(picked);

        await dialog.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(picked, "Keep"), dialog.Folder);
    }

    [Fact]
    public async Task ValidationWaitsForTheTypingToPauseAndCreateStaysDisabledUntilThen()
    {
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Location = TempRoot();

        dialog.Name = "Demo";

        Assert.True(dialog.IsValidating);
        Assert.False(dialog.CreateCommand.CanExecute(null));
        time.Advance(NewProjectDialogViewModel.ValidationDelay - TimeSpan.FromMilliseconds(1));
        Assert.True(dialog.IsValidating);
        Assert.False(dialog.CreateCommand.CanExecute(null));

        time.Advance(TimeSpan.FromMilliseconds(1));
        await dialog.ValidationTask;

        Assert.False(dialog.IsValidating);
        Assert.Null(dialog.Message);
        Assert.True(dialog.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task ANewKeystrokeCancelsThePreviousValidation()
    {
        string location = TempRoot();
        Directory.CreateDirectory(Path.Combine(location, "Taken"));
        File.WriteAllText(Path.Combine(location, "Taken", "notes.txt"), "mine");
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Location = location;

        dialog.Name = "Taken";
        time.Advance(TimeSpan.FromMilliseconds(200));
        dialog.Name = "Free";
        time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.True(dialog.IsValidating);
        Assert.Null(dialog.Message);
        time.Advance(TimeSpan.FromMilliseconds(50));
        await dialog.ValidationTask;
        Assert.Null(dialog.Message);
        Assert.True(dialog.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task ARelativeLocationIsRejectedAndNeverResolvedAgainstTheWorkingFolder()
    {
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Location = "projects/here";
        dialog.Name = "Demo";
        await SettleAsync(dialog);

        Assert.Contains("full path", dialog.Message, StringComparison.Ordinal);
        Assert.False(dialog.CreateCommand.CanExecute(null));
        Assert.False(Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "projects")));
    }

    [Fact]
    public async Task ATildeLocationExpandsToTheHomeFolderInThePreviewAndWhatIsRemembered()
    {
        string home = TempRoot();
        JsonEditorStateStore store = Store();
        var locations = new ProjectLocations(store, TempRoot(), home);
        NewProjectDialogViewModel dialog = Dialog(locations);
        dialog.Location = "~/Work";
        dialog.Name = "Tilde";
        await SettleAsync(dialog);

        Assert.Equal(Path.Combine(home, "Work", "Tilde"), dialog.Folder);
        Assert.Null(dialog.Message);

        await dialog.CreateCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(home, "Work", "Tilde", "Tilde.csproj")));
        Assert.Equal(Path.Combine(home, "Work"), store.LoadStart()?.NewProjectLocation);
    }

    [Fact]
    public async Task CancellingAfterTheFilesWereWrittenRemovesTheFolderTheDialogCreated()
    {
        string location = TempRoot();
        string folder = Path.Combine(location, "Half");
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.Projects.BeforeApply = async token =>
        {
            reached.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.SelectedTemplate = dialog.Templates.Single(template => template.Id == "netprints.template.library");
        dialog.Location = location;
        dialog.Name = "Half";
        await SettleAsync(dialog);

        Task creation = dialog.CreateCommand.ExecuteAsync(null);
        await reached.Task;
        Assert.True(File.Exists(Path.Combine(folder, "Half.csproj")));
        dialog.CancelCommand.Execute(null);
        await creation;

        Assert.False(Directory.Exists(folder));
        Assert.True(Directory.Exists(location));
        Assert.Null(dialog.Result);
    }

    [Fact]
    public async Task CancellingKeepsAFolderThatExistedBeforeAndRemovesOnlyWhatWasWritten()
    {
        string location = TempRoot();
        string folder = Path.Combine(location, "Pre");
        Directory.CreateDirectory(folder);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.Projects.BeforeApply = async token =>
        {
            reached.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.SelectedTemplate = dialog.Templates.Single(template => template.Id == "netprints.template.library");
        dialog.Location = location;
        dialog.Name = "Pre";
        await SettleAsync(dialog);

        Task creation = dialog.CreateCommand.ExecuteAsync(null);
        await reached.Task;
        dialog.CancelCommand.Execute(null);
        await creation;

        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public async Task ClosingTheWindowDuringCreationCancelsItAndRemovesTheProject()
    {
        string location = TempRoot();
        string folder = Path.Combine(location, "Closed");
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.Projects.BeforeApply = async token =>
        {
            reached.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.SelectedTemplate = dialog.Templates.Single(template => template.Id == "netprints.template.library");
        dialog.Location = location;
        dialog.Name = "Closed";
        await SettleAsync(dialog);

        Task creation = dialog.CreateCommand.ExecuteAsync(null);
        await reached.Task;
        await dialog.CancelCreationAsync();
        await creation;

        Assert.False(Directory.Exists(folder));
    }
}
