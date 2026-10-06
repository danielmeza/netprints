using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.State;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>New project (FR-042): the templates, the validation before anything is written, the seeded Program graph, the cleanup of a failed creation and the dialog.</summary>
public sealed class ProjectTemplateServiceTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private string TempRoot()
    {
        string root = TestPaths.CreateTempDirectory();
        cleanup.Add(root);
        return root;
    }

    private static ContributionRegistry Registry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        return registry;
    }

    private ProjectTemplateService Service(ContributionRegistry registry) =>
        new(() => registry.ProjectTemplates, id => id == DefaultProjectProfile.ProfileId ? DefaultProjectProfile.Instance : null, editor.Projects);

    private static ProjectTemplateDescriptor Template(ContributionRegistry registry, string id) => registry.ProjectTemplates.Single(template => template.Id == id);

    [Fact]
    public void TheBuiltInTemplatesUseTheDefaultProfile()
    {
        IReadOnlyList<ProjectTemplateDescriptor> templates = Registry().ProjectTemplates;

        Assert.Equal(["netprints.template.console", "netprints.template.library"], templates.Select(template => template.Id));
        Assert.Equal([ProjectOutputType.Console, ProjectOutputType.Library], templates.Select(template => template.OutputType));
        Assert.All(templates, template => Assert.Equal(DefaultProjectProfile.ProfileId, template.ProfileId));
    }

    [Fact]
    public async Task TheConsoleTemplateCreatesTheProjectAndSeedsTheProgramGraph()
    {
        ContributionRegistry registry = Registry();
        string folder = Path.Combine(TempRoot(), "Demo");

        string csproj = await Service(registry).CreateAsync(Template(registry, "netprints.template.console"), "Demo", folder, Token);

        Assert.Equal(Path.Combine(folder, "Demo.csproj"), csproj);
        Assert.True(File.Exists(csproj));
        string graphPath = Path.Combine(folder, "Program.netpc.json");
        using JsonDocument graph = JsonDocument.Parse(await File.ReadAllTextAsync(graphPath, Token));
        Assert.Equal("Demo", graph.RootElement.GetProperty("namespace").GetString());
        Assert.Equal("Program", graph.RootElement.GetProperty("name").GetString());
        JsonElement main = Assert.Single(graph.RootElement.GetProperty("methods").EnumerateArray());
        Assert.Equal("Main", main.GetProperty("name").GetString());
        Assert.Equal("Static", main.GetProperty("modifiers").GetString());
        Assert.Equal("Public", main.GetProperty("visibility").GetString());
        Assert.Empty(editor.Projects.ApplyCalls);

        // A fresh fake system scans the folder for graph files, as MSBuild evaluation does for a real project.
        ProjectLoadResult loaded = await TestEditor.CreatePersistence(new FakeProjectSystem()).LoadAsync(csproj, Token);
        Assert.Empty(loaded.Issues);
        Assert.Equal("Demo.Program", Assert.Single(loaded.Project.Classes).FullName);
    }

    [Fact]
    public async Task TheLibraryTemplateIsALibraryAndSeedsNoGraph()
    {
        ContributionRegistry registry = Registry();
        string folder = Path.Combine(TempRoot(), "Lib");

        string csproj = await Service(registry).CreateAsync(Template(registry, "netprints.template.library"), "Lib", folder, Token);

        Assert.True(File.Exists(csproj));
        Assert.Empty(Directory.GetFiles(folder, "*.netpc.json"));
        IReadOnlyList<ProjectEdit> edits = Assert.Single(editor.Projects.ApplyCalls);
        Assert.Equal(BinaryType.SharedLibrary, Assert.IsType<ProjectEdit.SetOutputType>(Assert.Single(edits)).Value);
    }

    [Fact]
    public async Task ATemplateRegisteredThroughTheRegistryAppearsAndCreatesItsProject()
    {
        ContributionRegistry registry = Registry();
        registry.AddProjectTemplate(new ProjectTemplateDescriptor("ext.template.game", "Game", "A game project", DefaultProjectProfile.ProfileId, ProjectOutputType.Library));
        ProjectTemplateService service = Service(registry);
        var dialog = new NewProjectDialogViewModel(service, editor.FilePicker);

        Assert.Contains(service.Templates, template => template.Id == "ext.template.game");
        Assert.Contains(dialog.Templates, template => template.Id == "ext.template.game");

        string folder = Path.Combine(TempRoot(), "Game");
        string csproj = await service.CreateAsync(Template(registry, "ext.template.game"), "Game", folder, Token);

        Assert.Contains(DefaultProjectProfile.ProfileId, await File.ReadAllTextAsync(csproj, Token), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1Demo")]
    [InlineData("My Project")]
    [InlineData("My..App")]
    [InlineData("a/b")]
    [InlineData("class")]
    [InlineData("Demo.")]
    [InlineData("con")]
    public async Task AnInvalidNameIsRejectedBeforeAnythingIsWritten(string name)
    {
        ContributionRegistry registry = Registry();
        ProjectTemplateService service = Service(registry);
        string folder = Path.Combine(TempRoot(), "Out");

        Assert.NotNull(service.Validate(name, folder));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.CreateAsync(Template(registry, "netprints.template.console"), name, folder, Token));
        Assert.False(Directory.Exists(folder));
        Assert.Empty(editor.Projects.ApplyCalls);
    }

    [Theory]
    [InlineData("Demo")]
    [InlineData("My.App")]
    [InlineData("_private")]
    public void AValidNameIsAccepted(string name) => Assert.Null(Service(Registry()).Validate(name, Path.Combine(TempRoot(), "Out")));

    [Fact]
    public async Task AFolderThatIsNotEmptyIsRejectedBeforeAnythingIsWritten()
    {
        ContributionRegistry registry = Registry();
        ProjectTemplateService service = Service(registry);
        string folder = Path.Combine(TempRoot(), "Busy");
        Directory.CreateDirectory(folder);
        string existing = Path.Combine(folder, "keep.txt");
        await File.WriteAllTextAsync(existing, "mine", Token);

        Assert.NotNull(service.Validate("Demo", folder));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.CreateAsync(Template(registry, "netprints.template.console"), "Demo", folder, Token));
        Assert.Equal([existing], Directory.GetFileSystemEntries(folder));
        Assert.Equal("mine", await File.ReadAllTextAsync(existing, Token));
    }

    [Fact]
    public async Task AFailingTemplateRemovesTheFolderItCreatedAndRethrows()
    {
        ContributionRegistry registry = Registry();
        editor.Projects.FailApply = _ => new IOException("boom");
        string folder = Path.Combine(TempRoot(), "Failing");

        IOException error = await Assert.ThrowsAsync<IOException>(() =>
            Service(registry).CreateAsync(Template(registry, "netprints.template.library"), "Failing", folder, Token));

        Assert.Equal("boom", error.Message);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task AFailingTemplateEmptiesAnExistingEmptyFolderButKeepsIt()
    {
        ContributionRegistry registry = Registry();
        editor.Projects.FailApply = _ => new IOException("boom");
        string folder = Path.Combine(TempRoot(), "Existing");
        Directory.CreateDirectory(folder);

        await Assert.ThrowsAsync<IOException>(() =>
            Service(registry).CreateAsync(Template(registry, "netprints.template.library"), "Existing", folder, Token));

        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public async Task ATemplateWhoseProfileIsUnknownWritesNothing()
    {
        ContributionRegistry registry = Registry();
        registry.AddProjectTemplate(new ProjectTemplateDescriptor("ext.template.lost", "Lost", "", "ext.profile.missing", ProjectOutputType.Console));
        string folder = Path.Combine(TempRoot(), "Lost");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(registry).CreateAsync(Template(registry, "ext.template.lost"), "Lost", folder, Token));

        Assert.False(Directory.Exists(folder));
    }

    private NewProjectDialogViewModel NewDialog(ContributionRegistry? registry = null) =>
        new(Service(registry ?? Registry()), editor.FilePicker);

    [Fact]
    public void CreateIsEnabledOnlyWhenTheInputIsValid()
    {
        NewProjectDialogViewModel dialog = NewDialog();
        Assert.False(dialog.CreateCommand.CanExecute(null));
        Assert.Null(dialog.Message);

        dialog.Name = "1bad";
        dialog.Folder = Path.Combine(TempRoot(), "Out");
        Assert.False(dialog.CreateCommand.CanExecute(null));
        Assert.NotNull(dialog.Message);

        dialog.Name = "Good";
        Assert.True(dialog.CreateCommand.CanExecute(null));
        Assert.Null(dialog.Message);
        Assert.Equal("netprints.template.console", dialog.SelectedTemplate?.Id);
    }

    [Fact]
    public async Task CreatingFromTheDialogClosesItWithTheProjectPath()
    {
        NewProjectDialogViewModel dialog = NewDialog();
        dialog.Name = "FromDialog";
        dialog.Folder = Path.Combine(TempRoot(), "FromDialog");

        await dialog.CreateCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(dialog.Folder, "FromDialog.csproj"), dialog.Result);
        Assert.True(File.Exists(dialog.Result));
    }

    [Fact]
    public async Task AFailedCreationShowsTheErrorKeepsTheDialogOpenAndRemovesTheFolder()
    {
        editor.Projects.FailApply = _ => new IOException("disk full");
        NewProjectDialogViewModel dialog = NewDialog();
        dialog.SelectedTemplate = dialog.Templates.Single(template => template.Id == "netprints.template.library");
        dialog.Name = "Broken";
        dialog.Folder = Path.Combine(TempRoot(), "Broken");

        await dialog.CreateCommand.ExecuteAsync(null);

        Assert.Null(dialog.Result);
        Assert.Contains("disk full", dialog.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(dialog.Folder));
    }

    [Fact]
    public async Task BrowseFillsTheFolderFromThePicker()
    {
        NewProjectDialogViewModel dialog = NewDialog();
        string picked = Path.Combine(TempRoot(), "Picked");
        editor.FilePicker.FolderAnswers.Enqueue(picked);

        await dialog.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(picked, dialog.Folder);

        editor.FilePicker.FolderAnswers.Enqueue(null);
        await dialog.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(picked, dialog.Folder);
    }

    private (ProjectRig Rig, RecentProjects Recent) NewRig()
    {
        var fs = new InMemoryEditorFileSystem();
        var store = new JsonEditorStateStore(new EditorDataPaths("/state-root"), fs, NullLogger.Instance);
        var recent = new RecentProjects(store, new RealEditorFileSystem(), new FakeTimeProvider());
        var rig = new ProjectRig(editor.Context with { Recent = recent });
        rigs.Add(rig);
        return (rig, recent);
    }

    [Fact]
    public async Task ASuccessfulNewProjectOpensItAndRecordsItInRecent()
    {
        (ProjectRig rig, RecentProjects recent) = NewRig();
        string folder = Path.Combine(TempRoot(), "Opened");
        editor.Dialogs.NewProjectScript = async dialog =>
        {
            dialog.Name = "Opened";
            dialog.Folder = folder;
            await dialog.CreateCommand.ExecuteAsync(null);
        };

        await rig.Actions.NewProjectAsync(Token);

        Assert.Equal(Path.Combine(folder, "Opened.csproj"), rig.Project?.Path);
        Assert.Equal(Path.Combine(folder, "Opened.csproj"), Assert.Single(recent.List()).Path);
    }

    [Fact]
    public async Task ACancelledNewProjectKeepsThePreviousProjectAndWritesNothing()
    {
        (ProjectRig rig, RecentProjects recent) = NewRig();
        string existing = TestPaths.CopyHelloWorldSample();
        cleanup.Add(existing);
        await rig.LoadProjectAsync(existing);
        ProjectSessionViewModel previous = Assert.IsType<ProjectSessionViewModel>(rig.Session);
        editor.Dialogs.NewProjectScript = dialog =>
        {
            dialog.CancelCommand.Execute(null);
            return Task.CompletedTask;
        };

        await rig.Actions.NewProjectAsync(Token);

        Assert.Same(previous, rig.Session);
        Assert.Equal([existing], recent.List().Select(entry => entry.Path));
    }
}
