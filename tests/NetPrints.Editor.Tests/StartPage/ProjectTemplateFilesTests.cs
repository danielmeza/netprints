using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>FR-025, NPXE0012: template files with the <c>dotnet new</c> replacement model, and the built-in trees that stay as P3a's New project wrote them.</summary>
public sealed class ProjectTemplateFilesTests : IDisposable
{
    private const string ConsoleTree =
        "Demo.csproj 623da7baca1e265560de5ea516aa2e4ffc53d2f72f3c574d25869fdb29d4c28f | Program.netpc.json fa7e02f275a6ff0f40436d4626fbce5d58ee22e8dd86d94b8eb04c8b503fb8d8";

    private const string LibraryTree = "Lib.csproj 8ffa02f72da8e9054e11b53bf4e315ea0ea7029f53d74bf0d921fb1bb2b6d83f";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string TempRoot()
    {
        string root = TestPaths.CreateTempDirectory();
        cleanup.Add(root);
        return root;
    }

    private ProjectTemplateService Service(ContributionRegistry registry) =>
        new(() => registry.ProjectTemplates, id => id == DefaultProjectProfile.ProfileId ? DefaultProjectProfile.Instance : null, editor.Projects);

    private static ContributionRegistry Registry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        return registry;
    }

    private static ProjectTemplateDescriptor WithFiles(params TemplateFile[] files) =>
        new("ext.template.files", "Files", "A template with files", DefaultProjectProfile.ProfileId, ProjectOutputType.Library, Files: files);

    private static string Tree(string folder) =>
        string.Join(
            " | ",
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .Select(path => $"{path} {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, path))))}"));

    [Fact]
    public async Task TheConsoleTemplateWritesTheTreeP3aWrote()
    {
        ContributionRegistry registry = Registry();
        string folder = Path.Combine(TempRoot(), "Demo");

        await Service(registry).CreateAsync(registry.ProjectTemplates.Single(t => t.Id == "netprints.template.console"), "Demo", folder, Token);

        Assert.Equal(ConsoleTree, Tree(folder));
    }

    [Fact]
    public async Task TheLibraryTemplateWritesTheTreeP3aWrote()
    {
        ContributionRegistry registry = Registry();
        string folder = Path.Combine(TempRoot(), "Lib");

        await Service(registry).CreateAsync(registry.ProjectTemplates.Single(t => t.Id == "netprints.template.library"), "Lib", folder, Token);

        Assert.Equal(LibraryTree, Tree(folder));
    }

    [Fact]
    public async Task TheSourceNameAndTheSymbolsAreReplacedInPathsAndText()
    {
        ContributionRegistry registry = Registry();
        var template = WithFiles(
            new TemplateFile("src/NetPrintsProject.Core.cs", "namespace NetPrintsRootNamespace; // NetPrintsProject on NetPrintsProfileId"));
        string folder = Path.Combine(TempRoot(), "My.App");

        await Service(registry).CreateAsync(template, "My.App", folder, Token);

        Assert.True(File.Exists(Path.Combine(folder, "src", "My.App.Core.cs")));
        string text = await File.ReadAllTextAsync(Path.Combine(folder, "src", "My.App.Core.cs"), Token);
        Assert.Equal($"namespace My.App; // My.App on {DefaultProjectProfile.ProfileId}", text);
    }

    [Fact]
    public async Task ALaunchSettingsFileIsWrittenLikeAnyOtherFile()
    {
        ContributionRegistry registry = Registry();
        var template = WithFiles(new TemplateFile("Properties/launchSettings.json", "{ \"profiles\": { \"NetPrintsProject\": { } } }"));
        string folder = Path.Combine(TempRoot(), "Game");

        await Service(registry).CreateAsync(template, "Game", folder, Token);

        Assert.True(File.Exists(Path.Combine(folder, "Properties", "launchSettings.json")));
        Assert.Equal("{ \"profiles\": { \"Game\": { } } }", await File.ReadAllTextAsync(Path.Combine(folder, "Properties", "launchSettings.json"), Token));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("a/../../escape.txt")]
    public async Task AFileThatLeavesTheProjectFolderIsRejectedAndNothingStays(string path)
    {
        ContributionRegistry registry = Registry();
        string folder = Path.Combine(TempRoot(), "Out");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(registry).CreateAsync(WithFiles(new TemplateFile(path, "x")), "Out", folder, Token));

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ARegisteredTemplateKeepsItsFiles()
    {
        ContributionRegistry registry = Registry();
        registry.AddProjectTemplate(WithFiles(new TemplateFile("a.txt", "A")));

        ProjectTemplateDescriptor stored = registry.ProjectTemplates.Single(t => t.Id == "ext.template.files");

        Assert.Equal("a.txt", Assert.Single(stored.Files ?? []).Path);
        await Task.CompletedTask;
    }
}
