using NetPrints.Core;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.References;

namespace NetPrints.Editor.Tests.References;

public class ReferenceListVMTests(TestEditor testEditor) : IDisposable
{
    private readonly string dir = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(dir);

    [Fact]
    public void ListsReferencesWithText()
    {
        var project = Project.CreateNew("P", "N");
        var vm = new ReferenceListVM(project, testEditor.Context);

        Assert.Equal(3, vm.References.Count());
        Assert.Contains("System.dll", vm.References[0].DisplayText);
    }

    [Fact]
    public async Task AddAssemblyIgnoresDuplicatesCaseInsensitive()
    {
        var editor = testEditor;
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);
        string dll = Path.Combine(dir, "Lib.dll");
        File.WriteAllText(dll, "");

        editor.FilePicker.OpenFileAnswers.Enqueue(dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        editor.FilePicker.OpenFileAnswers.Enqueue(OperatingSystem.IsWindows() ? dll.ToUpperInvariant() : dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        await vm.AddAssemblyAsync(Path.Combine(dir, ".", "Lib.dll"));

        Assert.Equal(1, vm.References.Count());
        Assert.False(vm.References[0].ShowIncludeInCompilation, "Include/Exclude is disabled for assemblies (PAR-19)");
    }

    [Fact]
    public async Task AddSourceDirectoryIgnoresDuplicatesAndTogglesInclude()
    {
        var editor = testEditor;
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);

        editor.FilePicker.FolderAnswers.Enqueue(dir);
        await vm.AddSourceDirectoryCommand.ExecuteAsync(null);
        await vm.AddSourceDirectoryAsync(dir + Path.DirectorySeparatorChar + ".");

        var reference = vm.References.Single();
        Assert.True(reference.ShowIncludeInCompilation);
        Assert.False(reference.IncludeInCompilation);
        reference.IncludeInCompilation = true;
        Assert.True(((SourceDirectoryReference)project.References.Single()).IncludeInCompilation);
    }

    [Fact]
    public async Task InvalidInputShowsErrorDialog()
    {
        var editor = testEditor;
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);

        await vm.AddAssemblyAsync("\0invalid");
        await vm.AddSourceDirectoryAsync("\0invalid");

        Assert.Equal(2, editor.Dialogs.Errors.Count());
        Assert.Empty(vm.References);
    }

    [Fact]
    public void RemoveReference()
    {
        var project = Project.CreateNew("P", "N");
        var vm = new ReferenceListVM(project, testEditor.Context);

        vm.RemoveCommand.Execute(vm.References[0]);

        Assert.Equal(2, project.References.Count());
        Assert.Equal(2, vm.References.Count());
    }
}
