using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Diagnostics;

namespace NetPrints.Editor.ErrorList;

/// <summary>
/// The class editor's Errors tab (FR-032, FR-034; editor-services.md §2): every diagnostic that
/// belongs to the open class, from the live analysis (<see cref="ICodeAnalysisHost.Snapshots"/>) and
/// the project's last build (<see cref="Project.LastDiagnostics"/>) combined. Double-clicking a
/// navigable row sends a <see cref="NavigateToNodeMessage"/> through the class editor's messenger
/// (FR-038: this view model depends only on the class graph, the live-analysis host and the
/// messenger, never the parent editor).
/// </summary>
public sealed partial class ErrorListVM : ObservableObject, IDisposable
{
    private readonly ClassGraph cls;
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
    public ErrorListVM(ClassGraph cls, ICodeAnalysisHost codeAnalysis, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(codeAnalysis);
        ArgumentNullException.ThrowIfNull(messenger);

        this.cls = cls;
        this.messenger = messenger;

        EnsureProjectSubscription();
        subscription = codeAnalysis.Snapshots.Subscribe(OnSnapshot);
    }

    /// <summary>Every diagnostic row of the open class, live analysis first then the last build's.</summary>
    public ObservableRangeCollection<DiagnosticRowVM> Rows { get; } = [];

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
    /// diagnostic with a member but no node mapping still opens the graph.
    /// </summary>
    /// <param name="row">Row to navigate to.</param>
    [RelayCommand]
    private void Navigate(DiagnosticRowVM? row)
    {
        if (row is not { CanNavigate: true } || row.Diagnostic.GraphKey is not { } graphKey)
        {
            return;
        }

        messenger.Send(new NavigateToNodeMessage(graphKey, row.Diagnostic.NodeId));
    }

    private void OnSnapshot(CodeAnalysisSnapshot snapshot)
    {
        liveDiagnostics = [.. snapshot.Diagnostics.Where(d => string.Equals(d.ClassFullName, cls.FullName, StringComparison.Ordinal))];
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
        if (cls.Project != currentProject)
        {
            if (currentProject is not null)
            {
                currentProject.PropertyChanged -= OnProjectPropertyChanged;
            }

            currentProject = cls.Project;
            if (currentProject is not null)
            {
                currentProject.PropertyChanged += OnProjectPropertyChanged;
            }
        }
    }

    private void Refresh()
    {
        EnsureProjectSubscription();
        IEnumerable<CodeDiagnostic> build = cls.Project?.LastDiagnostics
            .Where(d => string.Equals(d.ClassFullName, cls.FullName, StringComparison.Ordinal)) ?? [];
        Rows.ReplaceRange(liveDiagnostics.Concat(build).Select(d => new DiagnosticRowVM(d, cls)));
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
