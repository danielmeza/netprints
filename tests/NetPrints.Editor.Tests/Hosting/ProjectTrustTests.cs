using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// EX-T13: a project's <c>NetPrintsExtension</c> folders load only once the project is trusted. The extension
/// folders here hold an unreadable manifest, so a load attempt shows up as an <c>NPX001</c> failure while the
/// nodes of the untrusted extension stay preserved (the extension itself is covered by the loader tests).
/// </summary>
public class ProjectTrustTests : IDisposable
{
    private const string UnknownNode = """{ "$kind": "test.ext/widget", "id": "n000000000vny9" },""";

    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private string ProjectWithExtension(bool declareItem)
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);

        string graph = Path.Combine(Path.GetDirectoryName(csproj) ?? "", "HelloWorld.Program.netpc.json");
        File.WriteAllText(graph, File.ReadAllText(graph).Replace(
            """{ "$kind": "return", "id": "n000000000vny2" },""", """{ "$kind": "return", "id": "n000000000vny2" },""" + UnknownNode));

        string extensionFolder = Path.Combine(Path.GetDirectoryName(csproj) ?? "", "ext");
        Directory.CreateDirectory(extensionFolder);
        File.WriteAllText(Path.Combine(extensionFolder, ExtensionManifest.FileName), "{ this is not a manifest");

        ProjectSnapshot snapshot = testEditor.Projects.LoadAsync(csproj, CancellationToken.None).GetAwaiter().GetResult();
        testEditor.Projects.Seed(snapshot with { ExtensionFolders = declareItem ? [extensionFolder] : [] });
        testEditor.Projects.LoadCalls.Clear();
        return csproj;
    }

    private NetPrintsSettings Trusted => testEditor.Settings.Get(NetPrintsSettings.Descriptor);

    private IEnumerable<string> FailedExtensionCodes() =>
        testEditor.Extensions.Current.Results.OfType<ExtensionLoadResult.Failed>().Select(f => f.Code);

    [Fact]
    public async Task DecliningOpensTheProjectWithoutItsExtensionsAndReportsNpd006()
    {
        string csproj = ProjectWithExtension(declareItem: true);
        var vm = new MainEditorVM(testEditor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.Equal(csproj, testEditor.Dialogs.TrustCalls.Single().ProjectPath);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(csproj) ?? "", "ext"), testEditor.Dialogs.TrustCalls.Single().Folders.Single());
        Assert.NotNull(vm.Project);
        Assert.Empty(FailedExtensionCodes());
        Assert.Empty(testEditor.Dialogs.IssueDialogs);
        Assert.Empty(Trusted.TrustedProjects);
        Assert.Equal(0, testEditor.Settings.Writes);

        string message = testEditor.Dialogs.Errors.Single(e => e.Title == "Project loaded with issues").Message;
        Assert.Contains("NPD006", message);
        Assert.Contains("NPD001", message); // the node of the extension that was not loaded is preserved, not dropped
    }

    [Fact]
    public async Task TrustingLoadsTheExtensionsAndRecordsTheProject()
    {
        string csproj = ProjectWithExtension(declareItem: true);
        testEditor.Dialogs.TrustAnswer = true;
        var vm = new MainEditorVM(testEditor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.Equal([csproj], Trusted.TrustedProjects);
        Assert.Equal([ExtensionDiagnosticCodes.InvalidManifest], FailedExtensionCodes());
        Assert.DoesNotContain(testEditor.Dialogs.Errors, e => e.Message.Contains("NPD006", StringComparison.Ordinal));
        Assert.Contains("NPX001", testEditor.Dialogs.IssueDialogs.Single().Issues.Single().Id);
    }

    [Fact]
    public async Task ATrustedProjectLoadsItsExtensionsWithoutAsking()
    {
        string csproj = ProjectWithExtension(declareItem: true);
        await testEditor.Settings.SetAsync(NetPrintsSettings.Descriptor,
            NetPrintsSettings.Empty with { TrustedProjects = [csproj] }, TestContext.Current.CancellationToken);
        var vm = new MainEditorVM(testEditor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.Empty(testEditor.Dialogs.TrustCalls);
        Assert.Equal([ExtensionDiagnosticCodes.InvalidManifest], FailedExtensionCodes());
    }

    [Fact]
    public async Task AManifestInTheProjectFolderWithoutAnItemIsNeverLoaded()
    {
        string csproj = ProjectWithExtension(declareItem: false);
        var vm = new MainEditorVM(testEditor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.NotNull(vm.Project);
        Assert.Empty(testEditor.Dialogs.TrustCalls);
        Assert.Empty(FailedExtensionCodes());
        Assert.Empty(testEditor.Dialogs.IssueDialogs);
    }

    [Fact]
    public async Task OpeningAProjectWithoutExtensionsDropsThePreviousProjectsExtensions()
    {
        string withExtension = ProjectWithExtension(declareItem: true);
        testEditor.Dialogs.TrustAnswer = true;
        var vm = new MainEditorVM(testEditor.Context);
        await vm.LoadProjectAsync(withExtension);
        Assert.NotEmpty(FailedExtensionCodes());

        string plain = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(plain) ?? plain);
        await vm.LoadProjectAsync(plain);

        Assert.Empty(FailedExtensionCodes());
    }
}
