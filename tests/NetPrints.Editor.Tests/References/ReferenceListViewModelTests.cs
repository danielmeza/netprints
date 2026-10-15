using NetPrints.Core;
using NetPrints.Editor.References;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.References;

public class ReferenceListViewModelTests(TestEditor testEditor) : IDisposable
{
    private readonly string dir = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(dir);

    private (Project Project, ReferenceListViewModel Vm) CreateVm(IReadOnlyList<ProjectReferenceInfo>? references = null)
    {
        string path = Path.Combine(dir, "P.csproj");
        var snapshot = new ProjectSnapshot(path, "P", "N", "P", BinaryType.SharedLibrary, "net10.0",
            DefaultProjectProfile.ProfileId, true, [], [], [], references ?? [], [], "{}",
            new Dictionary<string, string>(), []);
        testEditor.Projects.Seed(snapshot);
        var project = Project.FromSnapshot(snapshot);
        return (project, new ReferenceListViewModel(project, testEditor.Context));
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
        await reference.SetIncludedCommand.ExecuteAsync(false);
        Assert.False(vm.References.Single().IncludeInCompilation);
    }

    [Fact]
    public void RealizingARowWithTheCurrentStateDoesNotApplyAnEdit()
    {
        // F-06: the toggle switch's OneWay binding sets IsChecked to the row's current state whenever a
        // row is (re)built, which raises IsCheckedChanged and invokes SetIncludedCommand with that same
        // state. That must be a no-op, not a redundant (or, before the fix, inverted) apply.
        var (_, vm) = CreateVm([
            new ProjectReferenceInfo(DeclaredReferenceKind.SourceDirectory, dir, null, true, true),
        ]);
        var reference = vm.References.Single();

        reference.SetIncludedCommand.Execute(reference.IncludeInCompilation);

        Assert.Empty(testEditor.Projects.ApplyCalls);
        Assert.True(vm.References.Single().IncludeInCompilation);
    }

    [Fact]
    public async Task ToggleAfterAFailedApplyEndsInTheClickedStateAndResyncsTheSwitch()
    {
        // F-06: SetIncludedCommand takes the switch's clicked state directly, and a failed apply rebuilds
        // References, so a second click ends in the state the user actually clicked rather than the
        // opposite (which `!reference.IncludeInCompilation` produced once the switch and model disagreed).
        var (_, vm) = CreateVm([
            new ProjectReferenceInfo(DeclaredReferenceKind.SourceDirectory, dir, null, true, true),
        ]);
        var reference = vm.References.Single();
        Assert.True(reference.IncludeInCompilation);

        bool failNext = true;
        testEditor.Projects.FailApply = _ =>
        {
            if (failNext)
            {
                failNext = false;
                return new InvalidOperationException("the csproj is locked");
            }

            return null;
        };

        // The user clicks to exclude; the apply fails, so the model is unchanged.
        await reference.SetIncludedCommand.ExecuteAsync(false);
        Assert.Single(testEditor.Dialogs.Errors);
        var resynced = vm.References.Single();
        Assert.NotSame(reference, resynced); // References was rebuilt so the switch resyncs with the model
        Assert.True(resynced.IncludeInCompilation);

        // The user clicks to exclude again; this time it applies.
        await resynced.SetIncludedCommand.ExecuteAsync(false);
        Assert.False(vm.References.Single().IncludeInCompilation); // ends in the state the user clicked
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
