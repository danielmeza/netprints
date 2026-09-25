using NetPrints.Core;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.References;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class ReferenceListVMTests
{
    private string dir = null!;

    [TestInitialize]
    public void Setup() => dir = TestPaths.CreateTempDirectory();

    [TestCleanup]
    public void Cleanup() => TestPaths.TryDelete(dir);

    [TestMethod]
    public void ListsReferencesWithText()
    {
        var project = Project.CreateNew("P", "N");
        var vm = new ReferenceListVM(project, new TestEditor().Context);

        Assert.HasCount(3, vm.References);
        StringAssert.Contains(vm.References[0].DisplayText, "System.dll");
    }

    [TestMethod]
    public async Task AddAssemblyIgnoresDuplicatesCaseInsensitive()
    {
        var editor = new TestEditor();
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);
        string dll = Path.Combine(dir, "Lib.dll");
        File.WriteAllText(dll, "");

        editor.FilePicker.OpenFileAnswers.Enqueue(dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        editor.FilePicker.OpenFileAnswers.Enqueue(OperatingSystem.IsWindows() ? dll.ToUpperInvariant() : dll);
        await vm.AddAssemblyCommand.ExecuteAsync(null);
        await vm.AddAssemblyAsync(Path.Combine(dir, ".", "Lib.dll"));

        Assert.HasCount(1, vm.References);
        Assert.IsFalse(vm.References[0].ShowIncludeInCompilation, "Include/Exclude is disabled for assemblies (PAR-19)");
    }

    [TestMethod]
    public async Task AddSourceDirectoryIgnoresDuplicatesAndTogglesInclude()
    {
        var editor = new TestEditor();
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);

        editor.FilePicker.FolderAnswers.Enqueue(dir);
        await vm.AddSourceDirectoryCommand.ExecuteAsync(null);
        await vm.AddSourceDirectoryAsync(dir + Path.DirectorySeparatorChar + ".");

        var reference = vm.References.Single();
        Assert.IsTrue(reference.ShowIncludeInCompilation);
        Assert.IsFalse(reference.IncludeInCompilation);
        reference.IncludeInCompilation = true;
        Assert.IsTrue(((SourceDirectoryReference)project.References.Single()).IncludeInCompilation);
    }

    [TestMethod]
    public async Task InvalidInputShowsErrorDialog()
    {
        var editor = new TestEditor();
        var project = Project.CreateNew("P", "N", addDefaultReferences: false);
        var vm = new ReferenceListVM(project, editor.Context);

        await vm.AddAssemblyAsync("\0invalid");
        await vm.AddSourceDirectoryAsync("\0invalid");

        Assert.HasCount(2, editor.Dialogs.Errors);
        Assert.IsEmpty(vm.References);
    }

    [TestMethod]
    public void RemoveReference()
    {
        var project = Project.CreateNew("P", "N");
        var vm = new ReferenceListVM(project, new TestEditor().Context);

        vm.RemoveCommand.Execute(vm.References[0]);

        Assert.HasCount(2, project.References);
        Assert.HasCount(2, vm.References);
    }
}
