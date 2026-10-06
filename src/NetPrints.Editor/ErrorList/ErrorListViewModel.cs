using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ErrorList;

/// <summary>
/// The Errors panel's list (FR-032, FR-034; editor-services.md §2): every diagnostic of the project, or, in the per-class mode only tests use, of one class, from the live analysis (<see cref="ICodeAnalysisHost.Snapshots"/>) and
/// the project's last build (<see cref="Project.LastDiagnostics"/>) combined. Double-clicking a
/// navigable row sends a <see cref="NavigateToNodeMessage"/> through the class editor's messenger
/// (FR-038: this view model depends only on the class graph, the live-analysis host and the
/// messenger, never the parent editor).
/// </summary>
public sealed partial class ErrorListViewModel : ObservableObject, IDisposable
{
    private readonly ClassGraph? cls;
    private readonly Project? wholeProject;
    private readonly IMessenger messenger;
    private readonly IDisposable subscription;
    private IReadOnlyList<CodeDiagnostic> liveDiagnostics = [];
    private Project? currentProject;

    /// <summary>
    /// Creates an error list that follows <paramref name="cls"/>'s live analysis and its project's
    /// last build.
    /// </summary>
    /// <param name="cls">Class to show the diagnostics of.</param>
    /// <param name="codeAnalysis">Live analysis host to follow.</param>
    /// <param name="messenger">Messenger to send <see cref="NavigateToNodeMessage"/> through.</param>
    public ErrorListViewModel(ClassGraph cls, ICodeAnalysisHost codeAnalysis, IMessenger messenger)
        : this(cls, null, codeAnalysis, messenger)
    {
        ArgumentNullException.ThrowIfNull(cls);
    }

    /// <summary>
    /// Creates an error list of every class of <paramref name="project"/>: its rows carry their class, and
    /// navigating sends <see cref="NavigateToNodeMessage.ClassFullName"/> too.
    /// </summary>
    /// <param name="project">Project to show the diagnostics of.</param>
    /// <param name="codeAnalysis">Live analysis host to follow.</param>
    /// <param name="messenger">Messenger to send <see cref="NavigateToNodeMessage"/> through.</param>
    public ErrorListViewModel(Project project, ICodeAnalysisHost codeAnalysis, IMessenger messenger)
        : this(null, project, codeAnalysis, messenger)
    {
        ArgumentNullException.ThrowIfNull(project);
    }

    private ErrorListViewModel(ClassGraph? cls, Project? wholeProject, ICodeAnalysisHost codeAnalysis, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(codeAnalysis);
        ArgumentNullException.ThrowIfNull(messenger);

        this.cls = cls;
        this.wholeProject = wholeProject;
        this.messenger = messenger;

        EnsureProjectSubscription();
        subscription = codeAnalysis.Snapshots.Subscribe(OnSnapshot);
        Refresh();
    }

    /// <summary>Every diagnostic row of the open class, live analysis first then the last build's.</summary>
    public ObservableRangeCollection<DiagnosticRowViewModel> Rows { get; } = [];

    /// <summary>Number of <see cref="Rows"/> with <see cref="CodeDiagnosticSeverity.Error"/> (FR-032, OWN-03).</summary>
    public int ErrorCount => Rows.Count(row => row.IsError);

    /// <summary>Number of <see cref="Rows"/> with <see cref="CodeDiagnosticSeverity.Warning"/> (FR-032, OWN-03).</summary>
    public int WarningCount => Rows.Count(row => row.IsWarning);

    /// <summary>Number of <see cref="Rows"/> with <see cref="CodeDiagnosticSeverity.Info"/> (FR-032, OWN-03).</summary>
    public int InfoCount => Rows.Count(row => row.IsInfo);

    /// <summary>
    /// Text for the class editor's "Errors" tab header (FR-032, OWN-03, owner report): eg. "Errors (2)
    /// · Warnings (1)", with an "· Info (n)" suffix only while there is at least one info diagnostic.
    /// </summary>
    public string Header
    {
        get
        {
            string header = $"Errors ({ErrorCount}) · Warnings ({WarningCount})";
            return InfoCount > 0 ? $"{header} · Info ({InfoCount})" : header;
        }
    }

    /// <summary>
    /// Opens a row's graph and, when it has one, reveals its node (FR-034, ED-T03, OWN-04): a
    /// diagnostic with a member but no node mapping still opens the graph, and one with only a class opens the class graph.
    /// </summary>
    /// <param name="row">Row to navigate to.</param>
    [RelayCommand]
    private void Navigate(DiagnosticRowViewModel? row)
    {
        if (row is not { CanNavigate: true })
        {
            return;
        }

        string graphKey = row.Diagnostic.GraphKey ?? DocumentId.ClassGraphKey;
        messenger.Send(new NavigateToNodeMessage(graphKey, row.Diagnostic.NodeId, wholeProject is null ? null : row.Diagnostic.ClassFullName));
    }

    private void OnSnapshot(CodeAnalysisSnapshot snapshot)
    {
        liveDiagnostics = [.. snapshot.Diagnostics.Where(Belongs)];
        Refresh();
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Project.LastDiagnostics))
        {
            Refresh();
        }
    }

    private void EnsureProjectSubscription()
    {
        Project? project = cls is null ? wholeProject : cls.Project;
        if (project != currentProject)
        {
            if (currentProject is not null)
            {
                currentProject.PropertyChanged -= OnProjectPropertyChanged;
            }

            currentProject = project;
            if (currentProject is not null)
            {
                currentProject.PropertyChanged += OnProjectPropertyChanged;
            }
        }
    }

    private bool Belongs(CodeDiagnostic diagnostic) => cls is null || string.Equals(diagnostic.ClassFullName, cls.FullName, StringComparison.Ordinal);

    private ClassGraph? OwnerOf(CodeDiagnostic diagnostic) =>
        cls ?? wholeProject?.Classes.FirstOrDefault(candidate => string.Equals(candidate.FullName, diagnostic.ClassFullName, StringComparison.Ordinal));

    private void Refresh()
    {
        EnsureProjectSubscription();
        IEnumerable<CodeDiagnostic> build = currentProject?.LastDiagnostics.Where(Belongs) ?? [];
        Rows.ReplaceRange(liveDiagnostics.Concat(build).Select(d => new DiagnosticRowViewModel(d, OwnerOf(d))));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InfoCount));
        OnPropertyChanged(nameof(Header));
    }

    /// <summary>Unsubscribes from the project's build result and the live-analysis host.</summary>
    public void Dispose()
    {
        if (currentProject is not null)
        {
            currentProject.PropertyChanged -= OnProjectPropertyChanged;
        }

        subscription.Dispose();
    }
}
