using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>NetPrints.Desktop --profile (FR-036, contracts/cli.md section 4): a known id filters New project, an unknown one says so, and a project argument wins.</summary>
public sealed class StartupProfileTests : IDisposable
{
    private const string Godot = "editor.profile/godot";

    private readonly List<string> cleanup = [];
    private readonly TestEditor editor;
    private readonly ProjectRig rig;

    public StartupProfileTests()
    {
        var manifest = new ExtensionManifest("editor.profile", "Profile test", "1.0.0", string.Empty, "1.0", []);
        var host = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new GodotExtension())]), NullLoggerFactory.Instance);
        editor = TestEditor.Create(TestEditor.CreateReflectionHost, host);
        rig = new ProjectRig(editor.Context);
    }

    public void Dispose()
    {
        rig.Dispose();
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private async Task<NewProjectDialogViewModel> NewProjectDialogAsync()
    {
        NewProjectDialogViewModel? shown = null;
        editor.Dialogs.NewProjectScript = dialog =>
        {
            shown = dialog;
            return Task.CompletedTask;
        };

        await rig.Actions.Loader.CreateProjectAsync();

        return shown ?? throw new InvalidOperationException("The New project dialog was not shown.");
    }

    [Fact]
    public async Task AKnownProfilePreselectsItInNewProjectAndFiltersTheTemplatesUntilShowAll()
    {
        await rig.Actions.Loader.OpenStartupProjectAsync(["--profile", Godot]);

        NewProjectDialogViewModel dialog = await NewProjectDialogAsync();

        Assert.Null(rig.Shell.StartPageError);
        Assert.True(dialog.IsFiltered);
        Assert.False(dialog.ShowAll);
        Assert.Equal([GodotExtension.TemplateId], dialog.Templates.Select(t => t.Id));
        Assert.Equal(GodotExtension.TemplateId, dialog.SelectedTemplate?.Id);

        dialog.ShowAll = true;

        Assert.True(dialog.Templates.Count > 1);
        Assert.Contains(dialog.Templates, t => t.ProfileId == DefaultProjectProfile.ProfileId);
        Assert.Contains(dialog.Templates, t => t.Id == GodotExtension.TemplateId);
    }

    [Fact]
    public async Task TheEqualsFormNamesTheProfileToo()
    {
        await rig.Actions.Loader.OpenStartupProjectAsync([$"--profile={Godot}"]);

        NewProjectDialogViewModel dialog = await NewProjectDialogAsync();

        Assert.Equal([GodotExtension.TemplateId], dialog.Templates.Select(t => t.Id));
    }

    [Fact]
    public async Task AnUnknownProfileLeavesTheTemplatesUnfilteredAndTheStartPageNamesTheIdAndTheKnownIds()
    {
        await rig.Actions.Loader.OpenStartupProjectAsync(["--profile", "no.such/profile"]);

        NewProjectDialogViewModel dialog = await NewProjectDialogAsync();

        string message = rig.Shell.StartPageError ?? throw new InvalidOperationException("No message was shown.");
        Assert.Contains("no.such/profile", message, StringComparison.Ordinal);
        Assert.Contains(DefaultProjectProfile.ProfileId, message, StringComparison.Ordinal);
        Assert.Contains(Godot, message, StringComparison.Ordinal);
        Assert.False(dialog.IsFiltered);
        Assert.True(dialog.Templates.Count > 1);
    }

    [Fact]
    public async Task AProfileWithoutAnIdIsReportedLikeAnUnknownOne()
    {
        await rig.Actions.Loader.OpenStartupProjectAsync(["--profile"]);

        Assert.Contains(Godot, rig.Shell.StartPageError ?? "", StringComparison.Ordinal);
        Assert.False((await NewProjectDialogAsync()).IsFiltered);
    }

    [Fact]
    public async Task WithAProjectPathTheProjectOpensAndTheProfileIsIgnoredWithAnInfoMessage()
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);

        await rig.Actions.Loader.OpenStartupProjectAsync(["--profile", Godot, csproj]);

        Assert.Equal(csproj, rig.Project?.Path);
        Assert.Null(rig.Shell.StartPageError);
        Assert.Contains(Godot, rig.Shell.StatusBar.Message ?? "", StringComparison.Ordinal);
        Assert.False((await NewProjectDialogAsync()).IsFiltered);
    }

    [Fact]
    public async Task WithoutTheArgumentNewProjectIsUnfiltered()
    {
        await rig.Actions.Loader.OpenStartupProjectAsync([]);

        NewProjectDialogViewModel dialog = await NewProjectDialogAsync();

        Assert.False(dialog.IsFiltered);
        Assert.True(dialog.Templates.Count > 1);
    }

    private sealed class GodotExtension : INetPrintsExtension
    {
        public const string TemplateId = "editor.profile/godot-template";

        public void Register(IExtensionBuilder builder) =>
            builder
                .AddProjectProfile(new GodotProfile())
                .AddProjectTemplate(new ProjectTemplateDescriptor(TemplateId, "Godot game", "A Godot project.", Godot, ProjectOutputType.Console));
    }

    private sealed class GodotProfile : IProjectProfile
    {
        public string Id => Godot;

        public string DisplayName => "Godot";

        public string DefaultTargetFramework => "net10.0";

        public string ProjectTemplate => string.Empty;

        public IReadOnlyList<TypeSpecifier> BaseTypes => [TypeSpecifier.FromType<object>()];

        public IReadOnlyList<ClassTemplate> ClassTemplates { get; } = [];

        public string? CatalogProfileId => null;
    }
}
