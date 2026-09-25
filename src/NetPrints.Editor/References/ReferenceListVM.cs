using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.ModelSync;

namespace NetPrints.Editor.References;

/// <summary>
/// View model of the References dialog (PAR-16..21).
/// </summary>
public sealed partial class ReferenceListVM : ObservableObject, IDisposable
{
    private readonly EditorContext context;

    public ReferenceListVM(Project project, EditorContext context)
    {
        Project = project;
        this.context = context;
        References = new ObservableViewModelCollection<CompilationReferenceVM, CompilationReference>(
            project.References, r => new CompilationReferenceVM(r));
    }

    public Project Project { get; }

    public ObservableViewModelCollection<CompilationReferenceVM, CompilationReference> References { get; }

    /// <summary>Adds an assembly; duplicates (full path, case-insensitive) are ignored (PAR-17).</summary>
    [RelayCommand]
    private async Task AddAssemblyAsync()
    {
        string? path = await context.FilePicker.OpenFileAsync("Add Assembly Reference", [FileFilter.Assemblies, FileFilter.AllFiles]);
        if (path is not null)
        {
            await AddAssemblyAsync(path);
        }
    }

    internal async Task AddAssemblyAsync(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            bool exists = Project.References.OfType<AssemblyReference>().Any(r =>
                r.AssemblyPath is not null
                && string.Equals(Path.GetFullPath(r.AssemblyPath), fullPath, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                Project.References.Add(new AssemblyReference(path));
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to add assembly", $"Failed to add assembly at {path}:\n\n{ex}");
        }
    }

    /// <summary>Adds a source directory; duplicates are ignored (PAR-18).</summary>
    [RelayCommand]
    private async Task AddSourceDirectoryAsync()
    {
        string? path = await context.FilePicker.OpenFolderAsync("Add Source Directory");
        if (path is not null)
        {
            await AddSourceDirectoryAsync(path);
        }
    }

    internal async Task AddSourceDirectoryAsync(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            bool exists = Project.References.OfType<SourceDirectoryReference>().Any(r =>
                string.Equals(Path.GetFullPath(r.SourceDirectory), fullPath, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                Project.References.Add(new SourceDirectoryReference(path));
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to add sources", $"Failed to add sources at {path}:\n\n{ex}");
        }
    }

    /// <summary>Removes a reference (PAR-20).</summary>
    [RelayCommand]
    private void Remove(CompilationReferenceVM? reference)
    {
        if (reference is not null)
        {
            Project.References.Remove(reference.Reference);
        }
    }

    public void Dispose() => References.Dispose();
}
