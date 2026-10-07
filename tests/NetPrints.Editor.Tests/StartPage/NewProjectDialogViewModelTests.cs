using Microsoft.Extensions.Logging.Abstractions;
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
        return new NewProjectDialogViewModel(service, editor.FilePicker, locations);
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
    public void TheProjectFolderIsTheLocationAndTheNameAndTheSecondLineOfTheDialogShowsIt()
    {
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        var changed = new List<string?>();
        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        string location = TempRoot();

        dialog.Location = location;
        dialog.Name = "Demo";

        Assert.Equal(Path.Combine(location, "Demo"), dialog.Folder);
        Assert.Contains(nameof(NewProjectDialogViewModel.Folder), changed);
        Assert.Null(dialog.Message);
        Assert.True(dialog.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void AFolderThatExistsAndIsNotEmptyIsRejectedAndAnEmptyOneIsAccepted()
    {
        string location = TempRoot();
        Directory.CreateDirectory(Path.Combine(location, "Taken"));
        File.WriteAllText(Path.Combine(location, "Taken", "notes.txt"), "mine");
        Directory.CreateDirectory(Path.Combine(location, "Empty"));
        NewProjectDialogViewModel dialog = Dialog(new ProjectLocations(Store(), TempRoot()));
        dialog.Location = location;

        dialog.Name = "Taken";
        Assert.Contains("not empty", dialog.Message, StringComparison.Ordinal);
        Assert.False(dialog.CreateCommand.CanExecute(null));

        dialog.Name = "Empty";
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
}
