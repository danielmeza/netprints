using NetPrints.Core;
using NetPrints.Editor.Main;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// EX-T07 (second half) and "New Class uses the project's profile": <c>NetPrintsProfile</c> picks the profile, an unknown
/// one falls back to the default profile with <c>NPD005</c>, and the <c>.csproj</c> is never rewritten.
/// </summary>
public sealed class ProjectProfileTests : IDisposable
{
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private static ExtensionHost HostWithTemplatedProfile()
    {
        var manifest = new ExtensionManifest("editor.test", "Editor test", "1.0.0", string.Empty, "1.0", []);
        return new ExtensionHost(new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new ProfileExtension())]),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
    }

    private async Task<(string Csproj, string Text)> OpenAsync(TestEditor editor, MainEditorVM vm, string profileId)
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);
        string text = await File.ReadAllTextAsync(csproj, TestContext.Current.CancellationToken);
        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(snapshot with { ProfileId = profileId });

        await vm.LoadProjectAsync(csproj);
        return (csproj, text);
    }

    [Fact]
    public async Task AnUnknownProfileFallsBackToTheDefaultProfileAndReportsNpd005WithoutTouchingTheCsproj()
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        var vm = new MainEditorVM(editor.Context);

        (string csproj, string before) = await OpenAsync(editor, vm, "unknown.profile");
        int classes = vm.Project?.Classes.Count ?? 0;
        await vm.NewClassCommand.ExecuteAsync(null);

        string message = editor.Dialogs.Errors.Single(e => e.Title == "Project loaded with issues").Message;
        Assert.Contains("NPD005", message);
        Assert.Contains("unknown.profile", message);
        Assert.Equal(before, await File.ReadAllTextAsync(csproj, TestContext.Current.CancellationToken));
        Assert.Equal(classes + 1, vm.Project?.Classes.Count);
        Assert.Equal("MyClass", vm.Project?.Classes[^1].Name);
        Assert.Equal(vm.Project?.DefaultNamespace, vm.Project?.Classes[^1].Namespace);
    }

    [Fact]
    public async Task ANewClassIsBuiltFromTheTemplateOfTheProjectsProfile()
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost, HostWithTemplatedProfile());
        var vm = new MainEditorVM(editor.Context);

        await OpenAsync(editor, vm, ProfileExtension.TemplatedProfile.ProfileId);
        await vm.NewClassCommand.ExecuteAsync(null);

        Assert.DoesNotContain(editor.Dialogs.Errors, e => e.Message.Contains("NPD005", StringComparison.Ordinal));
        ClassGraph created = vm.Project?.Classes[^1] ?? throw new InvalidOperationException("No class was created.");
        Assert.Equal("MyClass", created.Name);
        Assert.Equal("Templated", created.Namespace);
    }

    [Fact]
    public async Task TheTestExtensionsProfileIsFoundAndUsedForANewClassWithoutAWarning() // SC-004
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        editor.Dialogs.TrustAnswer = true;
        var vm = new MainEditorVM(editor.Context);
        string csproj = TestPaths.CopyHelloWorldSample();
        string projectDirectory = Path.GetDirectoryName(csproj) ?? csproj;
        cleanup.Add(projectDirectory);
        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(snapshot with { ProfileId = "netprints.test", ExtensionFolders = [TestExtensionFolder.CopyTo(projectDirectory)] });

        await vm.LoadProjectAsync(csproj);
        await vm.NewClassCommand.ExecuteAsync(null);

        Assert.NotNull(editor.Extensions.Current.FindProfile("netprints.test"));
        Assert.DoesNotContain(editor.Dialogs.Errors, e => e.Message.Contains("NPD005", StringComparison.Ordinal));
        ClassGraph created = vm.Project?.Classes[^1] ?? throw new InvalidOperationException("No class was created.");
        Assert.Equal("MyClass", created.Name);
    }

    [Fact]
    public async Task TheDefaultProfileIsUsedWithoutAWarning()
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        var vm = new MainEditorVM(editor.Context);

        await OpenAsync(editor, vm, DefaultProjectProfile.ProfileId);
        await vm.NewClassCommand.ExecuteAsync(null);

        Assert.Empty(editor.Dialogs.Errors);
        Assert.Equal(vm.Project?.DefaultNamespace, vm.Project?.Classes[^1].Namespace);
    }

    private sealed class ProfileExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddProjectProfile(new TemplatedProfile());

        public sealed class TemplatedProfile : IProjectProfile
        {
            public const string ProfileId = "editor.test/templated";

            public string Id => ProfileId;

            public string DisplayName => "Templated";

            public string DefaultTargetFramework => "net10.0";

            public string ProjectTemplate => string.Empty;

            public IReadOnlyList<TypeSpecifier> BaseTypes => [TypeSpecifier.FromType<object>()];

            public IReadOnlyList<ClassTemplate> ClassTemplates { get; } =
                [new ClassTemplate("editor.test/class", "Templated class", (project, name) => new ClassGraph { Name = name, Namespace = "Templated", Project = project })];

            public string? CatalogProfileId => null;
        }
    }
}
