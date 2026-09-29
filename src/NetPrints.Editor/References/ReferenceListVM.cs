using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.References;

/// <summary>
/// View model of the References dialog (PAR-16..21), working on <see cref="ProjectSnapshot.DeclaredReferences"/>
/// and applying every change through <see cref="IProjectSystem.ApplyAsync"/> (project-system.md §4).
/// </summary>
public sealed partial class ReferenceListVM : ObservableObject, IDisposable
{
    private readonly EditorContext context;

    /// <summary>
    /// Wraps <paramref name="project"/>'s declared references.
    /// </summary>
    /// <param name="project">Project whose references are shown and edited.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public ReferenceListVM(Project project, EditorContext context)
    {
        Project = project;
        this.context = context;
        RebuildReferences();
        ((INotifyPropertyChanged)project).PropertyChanged += OnProjectPropertyChanged;
    }

    /// <summary>The project whose references are shown and edited.</summary>
    public Project Project { get; }

    /// <summary>View models for <see cref="Project"/>'s declared references.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DeclaredReferenceVM> References { get; set; } = [];

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Core.Project.Snapshot))
        {
            RebuildReferences();
        }
    }

    private void RebuildReferences() =>
        References = (Project.Snapshot?.DeclaredReferences ?? [])
            .Select(reference => new DeclaredReferenceVM(reference))
            .ToList();

    /// <summary>Adds an assembly; a duplicate <c>HintPath</c> is a no-op (PAR-17, project-system.md §4).</summary>
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
            await ApplyAsync([new ProjectEdit.AddAssemblyReference(path)]);
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
            await ApplyAsync([new ProjectEdit.AddSourceDirectory(path)]);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to add sources", $"Failed to add sources at {path}:\n\n{ex}");
        }
    }

    /// <summary>
    /// Toggles a source directory reference between included (<c>Compile</c>) and excluded (<c>None</c>)
    /// (R2-10): the toggle switch's <c>OneWay</c> binding still shows <paramref name="reference"/>'s
    /// last-applied state, so the target state is its opposite.
    /// </summary>
    [RelayCommand]
    private async Task SetSourceDirectoryIncludedAsync(DeclaredReferenceVM? reference)
    {
        if (reference is not { Info.Kind: DeclaredReferenceKind.SourceDirectory })
        {
            return;
        }

        try
        {
            await ApplyAsync([new ProjectEdit.SetSourceDirectoryIncluded(reference.Info.Include, !reference.IncludeInCompilation)]);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to change the source directory", ex.ToString());
        }
    }

    /// <summary>Removes a reference (PAR-20).</summary>
    [RelayCommand]
    private async Task RemoveAsync(DeclaredReferenceVM? reference)
    {
        if (reference is null)
        {
            return;
        }

        try
        {
            await ApplyAsync([new ProjectEdit.RemoveReference(reference.Info.Kind, reference.Info.Include)]);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to remove the reference", ex.ToString());
        }
    }

    private async Task ApplyAsync(IReadOnlyList<ProjectEdit> edits)
    {
        ProjectSnapshot snapshot = await context.Projects.ApplyAsync(Project.Path, edits, CancellationToken.None);
        Project.Snapshot = snapshot;
        RebuildReferences();
    }

    /// <summary>Unsubscribes from <see cref="Project"/>.</summary>
    public void Dispose() => ((INotifyPropertyChanged)Project).PropertyChanged -= OnProjectPropertyChanged;
}
