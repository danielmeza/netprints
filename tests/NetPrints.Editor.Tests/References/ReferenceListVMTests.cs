using NetPrints.Core;
using NetPrints.Editor.References;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.References;

public class ReferenceListVMTests(TestEditor testEditor) : IDisposable
{
    private readonly string dir = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(dir);

    private (Project Project, ReferenceListVM Vm) CreateVm(IReadOnlyList<ProjectReferenceInfo>? references = null)
    {
        string path = Path.Combine(dir, "P.csproj");
        var snapshot = new ProjectSnapshot(path, "P", "N", "P", BinaryType.SharedLibrary, "net10.0",
            DefaultProjectProfile.ProfileId, true, [], [], [], references ?? [], [], "{}",
            new Dictionary<string, string>(), []);
        testEditor.Projects.Seed(snapshot);
        var project = Project.FromSnapshot(snapshot);
        return (project, new ReferenceListVM(project, testEditor.Context));
    }

    [Fact]
    public void ListsReferencesWithText()
    {
        var (_, vm) = CreateVm([
            new ProjectReferenceInfo(DeclaredReferenceKind.Package, "System.Text.Json", "8.0.0", true, false),
        ]);

        Assert.Single(vm.References);
        Assert.Contains("System.Text.Json", vm.References[0].DisplayText);
    }

    [Fact]
    public async Task AddAssemblyIgnoresDuplicatesCaseInsensitive()
    {
        var editor = testEditor;
        var (_, vm) = CreateVm();
        string dll = Path.Combine(dir, "Lib.dll");
        File.WriteAllText(dll, "");

        editor.FilePicker.OpenFileAnswers.Enqueue(dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        editor.FilePicker.OpenFileAnswers.Enqueue(OperatingSystem.IsWindows() ? dll.ToUpperInvariant() : dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        await vm.AddAssemblyAsync(Path.Combine(dir, ".", "Lib.dll"));

        Assert.Single(vm.References);
        Assert.False(vm.References[0].ShowIncludeInCompilation, "Include/Exclude is disabled for assemblies (PAR-19)");
    }

    [Fact]
    public async Task AddSourceDirectoryIgnoresDuplicatesAndTogglesInclude()
    {
        var editor = testEditor;
        var (_, vm) = CreateVm();

        editor.FilePicker.FolderAnswers.Enqueue(dir);
        await vm.AddSourceDirectoryCommand.ExecuteAsync(null);
        await vm.AddSourceDirectoryAsync(dir + Path.DirectorySeparatorChar + ".");

        var reference = vm.References.Single();
        Assert.True(reference.ShowIncludeInCompilation);
        Assert.True(reference.IncludeInCompilation, "a newly added source directory is a Compile item (project-system.md §4)");
        await vm.SetSourceDirectoryIncludedCommand.ExecuteAsync(reference);
        Assert.False(vm.References.Single().IncludeInCompilation);
    }

    [Fact]
    public async Task InvalidInputShowsErrorDialog()
    {
        var editor = testEditor;
        var (_, vm) = CreateVm();

        // A path with an embedded NUL is never a valid full path (Path.GetFullPath throws).
        await vm.AddAssemblyAsync("\0invalid");
        await vm.AddSourceDirectoryAsync("\0invalid");

        Assert.Equal(2, editor.Dialogs.Errors.Count());
        Assert.Empty(vm.References);
    }

    [Fact]
    public void RemoveReference()
    {
        var (_, vm) = CreateVm([
            new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "A", null, true, true),
            new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "B", null, true, true),
            new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "C", null, true, true),
        ]);

        vm.RemoveCommand.Execute(vm.References[0]);

        Assert.Equal(2, vm.References.Count());
        Assert.DoesNotContain(vm.References, r => r.Info.Include == "A");
    }
}
