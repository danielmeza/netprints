using NetPrints.Core;
using NetPrints.Editor.Main;
using NetPrints.Graph;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// The persistence follows the extension registry: once a project's extension is loaded the mapper knows its node
/// kinds, so a node of that kind is a real node instead of a preserved unknown one, and it is written back as its own kind.
/// </summary>
public sealed class ExtensionPersistenceTests : IDisposable
{
    private const string LogNode = """{ "$kind": "netprints.test/Log", "id": "n000000000vny9" },""";

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private async Task<(string Csproj, string GraphPath)> ProjectWithLogNodeAsync()
    {
        string csproj = TestPaths.CopyHelloWorldSample();
        string projectDirectory = Path.GetDirectoryName(csproj) ?? csproj;
        cleanup.Add(projectDirectory);

        string graphPath = Path.Combine(projectDirectory, "HelloWorld.Program.netpc.json");
        string original = await File.ReadAllTextAsync(graphPath, TestContext.Current.CancellationToken);
        string marker = """{ "$kind": "return", "id": "n000000000vny2" },""";
        Assert.Contains(marker, original, StringComparison.Ordinal);
        await File.WriteAllTextAsync(graphPath, original.Replace(marker, marker + LogNode, StringComparison.Ordinal), TestContext.Current.CancellationToken);

        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(snapshot with { ExtensionFolders = [TestExtensionFolder.CopyTo(projectDirectory)] });
        return (csproj, graphPath);
    }

    private static Node? FindLogNode(Project? project) =>
        project?.Classes.SelectMany(c => c.Methods).SelectMany(m => m.Nodes).FirstOrDefault(n => n.GetType().Name == "LogNode");

    [Fact]
    public async Task ATrustedExtensionsNodeIsLoadedAndSavedAsItsOwnKind()
    {
        (string csproj, string graphPath) = await ProjectWithLogNodeAsync();
        editor.Dialogs.TrustAnswer = true;
        var vm = new MainEditorVM(editor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.NotNull(FindLogNode(vm.Project));
        Assert.DoesNotContain(editor.Dialogs.Errors, e => e.Message.Contains("NPD001", StringComparison.Ordinal));

        vm.Project?.Classes.Single().MarkDirty();
        await editor.Persistence.SaveAsync(vm.Project ?? throw new InvalidOperationException("No project."), _ => "// generated", TestContext.Current.CancellationToken);
        Assert.Contains("\"netprints.test/Log\"", await File.ReadAllTextAsync(graphPath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeclinedExtensionsNodeIsPreservedNotLoaded()
    {
        (string csproj, string graphPath) = await ProjectWithLogNodeAsync();
        var vm = new MainEditorVM(editor.Context);

        await vm.LoadProjectAsync(csproj);

        Assert.Null(FindLogNode(vm.Project));
        Assert.Contains("NPD001", editor.Dialogs.Errors.Single(e => e.Title == "Project loaded with issues").Message, StringComparison.Ordinal);

        vm.Project?.Classes.Single().MarkDirty();
        await editor.Persistence.SaveAsync(vm.Project ?? throw new InvalidOperationException("No project."), _ => "// generated", TestContext.Current.CancellationToken);
        Assert.Contains("\"netprints.test/Log\"", await File.ReadAllTextAsync(graphPath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
