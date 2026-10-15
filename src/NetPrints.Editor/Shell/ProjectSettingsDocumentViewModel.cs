using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Shell;

/// <summary>The Project settings document (<c>project-settings</c>): the settings of the open project, today its output binary type.</summary>
public sealed partial class ProjectSettingsDocumentViewModel : DocumentViewModel
{
    private const string DocumentTitle = "Project settings";

    private readonly ProjectSessionViewModel session;
    private readonly EditorContext context;

    /// <summary>Creates the document of <paramref name="session"/>'s project.</summary>
    /// <param name="session">The open project session.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public ProjectSettingsDocumentViewModel(ProjectSessionViewModel session, EditorContext context)
        : base(DocumentId.ProjectSettings, DocumentTitle)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);
        this.session = session;
        this.context = context;
        session.Project.PropertyChanged += OnProjectChanged;
    }

    /// <summary>Gets the values offered by the binary type chooser.</summary>
    public IReadOnlyList<BinaryType> BinaryTypes { get; } = Enum.GetValues<BinaryType>();

    /// <summary>Gets the open project's output binary type.</summary>
    public BinaryType OutputBinaryType => session.Project.OutputBinaryType;

    /// <summary>Edits the project file's output type and replaces the project's snapshot once that completes.</summary>
    /// <param name="value">The new output type.</param>
    /// <returns>A task that completes when the edit has been applied.</returns>
    [RelayCommand]
    private async Task SetOutputTypeAsync(BinaryType value)
    {
        Project project = session.Project;
        if (project.OutputBinaryType == value)
        {
            return;
        }

        try
        {
            ProjectSnapshot snapshot = await context.Projects.ApplyAsync(project.Path, [new ProjectEdit.SetOutputType(value)], CancellationToken.None);
            project.Snapshot = snapshot;
            project.OutputBinaryType = snapshot.OutputType;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to change the binary type", ex.ToString());
        }
        finally
        {
            OnPropertyChanged(nameof(OutputBinaryType));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            session.Project.PropertyChanged -= OnProjectChanged;
        }

        base.Dispose(disposing);
    }

    private void OnProjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Project.OutputBinaryType))
        {
            OnPropertyChanged(nameof(OutputBinaryType));
        }
    }
}
