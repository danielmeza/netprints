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
            .Select(info => new DeclaredReferenceVM(info, included => SetSourceDirectoryIncludedAsync(info, included)))
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
    /// Sets a source directory reference's included/excluded state to <paramref name="included"/>
    /// (R2-10, F-06): the target state comes from the toggle switch's own clicked state
    /// (<see cref="DeclaredReferenceVM.SetIncludedCommand"/>), not from inverting the row's
    /// last-applied state, so a switch left out of sync by a prior failed apply cannot invert the next
    /// click. Realizing a row (the <c>OneWay</c> binding setting the switch to the current model state)
    /// calls this with <paramref name="included"/> already equal to <paramref name="info"/>'s state, which
    /// is a no-op below rather than an unwanted apply.
    /// </summary>
    private async Task SetSourceDirectoryIncludedAsync(ProjectReferenceInfo info, bool included)
    {
        if (info.Kind != DeclaredReferenceKind.SourceDirectory || included == info.Included)
        {
            return;
        }

        try
        {
            await ApplyAsync([new ProjectEdit.SetSourceDirectoryIncluded(info.Include, included)]);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to change the source directory", ex.ToString());
            RebuildReferences(); // resync every switch with the model after a failed apply
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
