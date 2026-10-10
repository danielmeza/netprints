using System.Collections.ObjectModel;
using System.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>The breadcrumbs above a graph document: Project, Class, Graph (FR-065). Choosing a segment reveals it in the project tree.</summary>
public sealed class BreadcrumbsViewModel : IDisposable
{
    private readonly ShellViewModel shell;
    private readonly List<INotifyPropertyChanged> watched = [];

    /// <summary>Creates the breadcrumbs of a graph.</summary>
    /// <param name="project">The open project.</param>
    /// <param name="graph">The graph the document shows.</param>
    /// <param name="shell">The shell whose rename notice refreshes the names.</param>
    /// <param name="reveal">Reveals a model in the project tree; returns whether it was found.</param>
    public BreadcrumbsViewModel(Project project, NodeGraphViewModel graph, ShellViewModel shell, Func<object, bool> reveal)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(reveal);
        this.shell = shell;
        List<BreadcrumbSegmentViewModel> segments = [new(project, () => project.Name, reveal)];
        if (CommandTargets.ClassOf(graph.Graph) is { } cls)
        {
            segments.Add(new(cls, () => cls.Name, reveal));
        }

        object member = CommandTargets.MemberOf(graph.Graph);
        if (!ReferenceEquals(member, graph.Graph) || graph.Graph is not ClassGraph)
        {
            segments.Add(new(member, () => graph.Name, reveal));
        }

        segments[^1].IsLast = true;
        Segments = new ReadOnlyObservableCollection<BreadcrumbSegmentViewModel>(new ObservableCollection<BreadcrumbSegmentViewModel>(segments));
        foreach (object model in new object?[] { project, CommandTargets.ClassOf(graph.Graph), graph.Graph }.OfType<object>())
        {
            if (model is INotifyPropertyChanged notifying)
            {
                notifying.PropertyChanged += OnModelChanged;
                watched.Add(notifying);
            }
        }

        shell.ModelRenamed += OnRenamed;
    }

    /// <summary>Gets the segments, outermost first.</summary>
    public ReadOnlyObservableCollection<BreadcrumbSegmentViewModel> Segments { get; }

    /// <summary>Builds the reveal function that shows the project tree and selects a model's row.</summary>
    /// <param name="shell">The shell that holds the project tree panel.</param>
    /// <param name="api">Shows the panel.</param>
    /// <returns>The function.</returns>
    public static Func<object, bool> RevealInTree(ShellViewModel shell, IShell api)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(api);
        return model =>
        {
            api.ShowPanel(PanelContributions.ProjectTreeId);
            return shell.FindPanel(PanelContributions.ProjectTreeId)?.Content is ProjectTreePanelViewModel tree && tree.Select(model);
        };
    }

    /// <summary>Stops following renames.</summary>
    public void Dispose()
    {
        shell.ModelRenamed -= OnRenamed;
        watched.ForEach(model => model.PropertyChanged -= OnModelChanged);
        watched.Clear();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnRenamed(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        foreach (BreadcrumbSegmentViewModel segment in Segments)
        {
            segment.Refresh();
        }
    }
}
