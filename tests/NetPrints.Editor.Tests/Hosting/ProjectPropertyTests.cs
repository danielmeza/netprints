using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// FR-025: an extension's project-scope setting is an MSBuild property, requested through
/// <see cref="ProjectSystemOptions.ExtraProperties"/> and read from <see cref="ProjectSnapshot.GetProperty"/>.
/// </summary>
public sealed class ProjectPropertyTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(directory);

    private string WriteProject(string properties)
    {
        string csproj = Path.Combine(directory, "Props.csproj");
        File.WriteAllText(csproj, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                {properties}
              </PropertyGroup>
            </Project>
            """);
        return csproj;
    }

    private static MsBuildProjectSystem NewSystem(IExtensionHost extensions) =>
        new(new ProjectSystemOptions(new ExtensionProjectProperties(extensions), "1.0.0-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

    [Fact]
    public async Task TheTestExtensionReadsItsPropertyThroughTheSnapshot()
    {
        await using ExtensionHost extensions = TestExtensionFolder.CreateHost();
        string csproj = WriteProject("<NetPrintsTestMode>on</NetPrintsTestMode>");

        ProjectSnapshot snapshot = await NewSystem(extensions).LoadAsync(csproj, TestContext.Current.CancellationToken);

        Assert.Contains("NetPrintsTestMode", extensions.Current.ProjectProperties);
        Assert.Equal("on", snapshot.GetProperty("NetPrintsTestMode"));
    }

    [Fact]
    public async Task ApropertyNoLoadedExtensionRequestsIsNotEvaluated()
    {
        await using ExtensionHost extensions = TestExtensions.CreateBuiltIn();
        string csproj = WriteProject("<NetPrintsTestMode>on</NetPrintsTestMode>");

        ProjectSnapshot snapshot = await NewSystem(extensions).LoadAsync(csproj, TestContext.Current.CancellationToken);

        Assert.Null(snapshot.GetProperty("NetPrintsTestMode"));
    }

    [Fact]
    public async Task AProjectSystemCreatedBeforeTheExtensionLoadsSeesItsPropertyAfterwards()
    {
        await using ExtensionHost extensions = TestExtensions.CreateBuiltIn();
        MsBuildProjectSystem system = NewSystem(extensions);
        string csproj = WriteProject("<NetPrintsTestMode>on</NetPrintsTestMode>");
        Assert.Null((await system.LoadAsync(csproj, TestContext.Current.CancellationToken)).GetProperty("NetPrintsTestMode"));

        await extensions.LoadForProjectAsync([TestExtensionFolder.Folder], CancellationToken.None);

        Assert.Equal("on", (await system.LoadAsync(csproj, TestContext.Current.CancellationToken)).GetProperty("NetPrintsTestMode"));
    }

    [Fact]
    public async Task TheEditorReloadsTheSnapshotWhenAProjectExtensionAddsProperties()
    {
        TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        string csproj = TestPaths.CopyHelloWorldSample();
        string projectDirectory = Path.GetDirectoryName(csproj) ?? csproj;
        ProjectSnapshot seeded = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(seeded with { ExtensionFolders = [TestExtensionFolder.CopyTo(projectDirectory)] });
        editor.Projects.LoadCalls.Clear();
        editor.Dialogs.TrustAnswer = true;

        try
        {
            await new MainEditorViewModel(editor.Context).LoadProjectAsync(csproj);

            Assert.Equal(2, editor.Projects.LoadCalls.Count);
            Assert.Contains("NetPrintsTestMode", editor.Extensions.Current.ProjectProperties);
        }
        finally
        {
            TestPaths.TryDelete(projectDirectory);
        }
    }

    [Fact]
    public async Task TheEditorLoadsTheSnapshotOnceWhenNoExtensionAddsProperties()
    {
        TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        string csproj = TestPaths.CopyHelloWorldSample();

        try
        {
            await new MainEditorViewModel(editor.Context).LoadProjectAsync(csproj);

            Assert.Single(editor.Projects.LoadCalls);
        }
        finally
        {
            TestPaths.TryDelete(csproj);
        }
    }
}
