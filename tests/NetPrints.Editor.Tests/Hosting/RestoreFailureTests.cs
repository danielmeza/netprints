using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>Review E R2: a failed SDK restore reaches the user instead of opening an empty project.</summary>
public sealed class RestoreFailureTests : IDisposable
{
    private const string Version = "0.2.1-alpha.0.5";

    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private async Task<string> OpenAsync(Func<ProjectSnapshot, ProjectSnapshot> change)
    {
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost);
        var rig = new ProjectRig(editor.Context);
        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);
        ProjectSnapshot snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
        editor.Projects.Seed(change(snapshot));

        await rig.LoadProjectAsync(csproj);

        return editor.Dialogs.Errors.Single(e => e.Title == "Project loaded with issues").Message;
    }

    private static ProjectSnapshot Restoring(ProjectSnapshot snapshot) => snapshot with
    {
        GraphFiles = [],
        DeclaredReferences = [new ProjectReferenceInfo(DeclaredReferenceKind.Package, "NetPrints.Sdk", Version, true, false)],
        Messages = [new ProjectMessage(ProjectMessageSeverity.Error, ProjectMessage.RestoreFailed, "error NU1102: Unable to find package NetPrints.Sdk", snapshot.ProjectFilePath, null, null)],
    };

    [Fact]
    public async Task AFailedRestoreIsShownWithThePackageTheVersionAndWhatToDo()
    {
        string message = await OpenAsync(Restoring);

        Assert.Contains(ProjectMessage.RestoreFailed, message, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Sdk " + Version, message, StringComparison.Ordinal);
        Assert.Contains("NU1102", message, StringComparison.Ordinal);
        Assert.Contains("package source", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphFilesOnDiskWithNoneLoadedSaySoInsteadOfOpeningAnEmptyTree()
    {
        string message = await OpenAsync(snapshot => snapshot with { GraphFiles = [] });

        Assert.Contains("no graphs were loaded", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOtherErrorMessageOfTheSnapshotIsShownToo()
    {
        string message = await OpenAsync(snapshot => snapshot with
        {
            Messages = [new ProjectMessage(ProjectMessageSeverity.Error, ProjectMessage.WorkspaceDiagnostic, "Could not open the project", null, null, null)],
        });

        Assert.Contains("Could not open the project", message, StringComparison.Ordinal);
    }
}
