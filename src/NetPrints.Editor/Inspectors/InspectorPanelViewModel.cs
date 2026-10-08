using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Inspectors;

/// <summary>
/// The inspector panel: hosts the class, method, variable, event graph or event entry inspector for the project tree's selection
/// (<see cref="ShellViewModel.TreeSelection"/>) or for the selection in the active graph
/// (<see cref="GraphSelectionInspectorTarget"/>), whichever changed last, and an empty state when nothing is selected or no project is open.
/// The inspectors come from the class's <see cref="ClassContext"/>, which the session owns.
/// </summary>
public sealed partial class InspectorPanelViewModel : ObservableObject, IShellPanelContent, IRecipient<SelectInspectorMessage>, IRecipient<SelectEventEntryMessage>
{
    private readonly HashSet<ClassContext> watched = [];
    private PanelContext? context;
    private NodeGraphViewModel? followedGraph;
    private string selectedNodes = "";

    /// <summary>Gets the view model of the inspector shown, or null for the empty state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial object? Content { get; private set; }

    partial void OnContentChanged(object? oldValue, object? newValue)
    {
        if (oldValue is EventEntryInspectorViewModel entryInspector && !ReferenceEquals(oldValue, newValue))
        {
            entryInspector.Dispose();
        }
    }

    /// <summary>Gets a value indicating whether nothing is inspected.</summary>
    public bool IsEmpty => Content is null;

    /// <summary>Gets the text of the empty state.</summary>
    public string EmptyMessage => "Select a class, method, variable or event graph to inspect it.";

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        context.Shell.PropertyChanged += OnShellChanged;
        Refresh();
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (context is { } attached)
        {
            attached.Shell.PropertyChanged -= OnShellChanged;
        }

        Content = null;
        FollowGraph(null);
        Unwatch();
        context = null;
    }

    /// <inheritdoc/>
    void IRecipient<SelectInspectorMessage>.Receive(SelectInspectorMessage message)
    {
        if (context is { } attached)
        {
            attached.Shell.TreeSelection = message.Target.Variable;
        }
    }

    /// <inheritdoc/>
    void IRecipient<SelectEventEntryMessage>.Receive(SelectEventEntryMessage message)
    {
        if (context is { Shell: { Session: { } session } shell } attached
            && CommandTargets.GraphDocumentOf(session, message.Entry.Graph) is { } id)
        {
            attached.Api.OpenDocument(id);
            if (shell.FindDocument(id) is GraphDocumentViewModel document)
            {
                document.Graph.RevealNode(message.Entry.Id);
            }
        }
    }

    private void OnInspectorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassInspectorViewModel.Name))
        {
            context?.Shell.NotifyModelRenamed();
        }
    }

    private void OnUndoApplied(object? sender, EventArgs e)
    {
        if (Content is EventGraphViewModel or EventEntryInspectorViewModel)
        {
            context?.Shell.NotifyModelRenamed();
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.Session):
                Content = null;
                FollowGraph(null);
                Unwatch();
                break;
            case nameof(ShellViewModel.ActiveDocument):
                FollowGraph((context?.Shell.ActiveDocument as GraphDocumentViewModel)?.Graph);
                break;
            case nameof(ShellViewModel.TreeSelection):
                Refresh();
                break;
        }
    }

    private void FollowGraph(NodeGraphViewModel? graph)
    {
        if (ReferenceEquals(graph, followedGraph))
        {
            return;
        }

        if (followedGraph is not null)
        {
            followedGraph.SelectionChanged -= OnGraphSelectionChanged;
        }

        followedGraph = graph;
        selectedNodes = graph is null ? "" : SelectionKey(graph);
        if (graph is not null)
        {
            graph.SelectionChanged += OnGraphSelectionChanged;
        }
    }

    private static string SelectionKey(NodeGraphViewModel graph) => string.Join('\n', graph.SelectedNodes.Select(node => node.Node.Id));

    private void OnGraphSelectionChanged(object? sender, EventArgs e)
    {
        if (followedGraph is not { } graph || context is not { Shell: { Session: not null } shell })
        {
            return;
        }

        string key = SelectionKey(graph);
        if (key == selectedNodes)
        {
            return;
        }

        selectedNodes = key;
        shell.TreeSelection = GraphSelectionInspectorTarget.Resolve(graph.Graph, [.. graph.SelectedNodes.Select(node => node.Node)]);
    }

    private void Refresh()
    {
        if (context is not { Shell: { Session: { } session } shell })
        {
            Content = null;
            return;
        }

        watched.RemoveWhere(classContext => classContext.IsDisposed);
        Content = InspectorOf(session, shell.TreeSelection);
    }

    private object? InspectorOf(ProjectSessionViewModel session, object? selection) => selection switch
    {
        ClassGraph cls => Watch(session.ContextFor(cls)).ClassInspector,
        MethodGraph { Class: { } owner } method => Watch(session.ContextFor(owner)).Methods.FirstOrDefault(item => ReferenceEquals(item.Graph, method)),
        ConstructorGraph { Class: { } owner } constructor => Watch(session.ContextFor(owner)).Constructors.FirstOrDefault(item => ReferenceEquals(item.Graph, constructor)),
        Variable { Class: { } owner } variable => Watch(session.ContextFor(owner)).Variables.FirstOrDefault(item => ReferenceEquals(item.Variable, variable)),
        EventGraph { Class: { } owner } eventGraph => EventGraphInspectorOf(Watch(session.ContextFor(owner)), eventGraph),
        EventEntryNode { Graph.Class: { } owner } entry => Watch(session.ContextFor(owner)).EventEntryInspectorOf(entry),
        _ => null,
    };

    private static EventGraphViewModel? EventGraphInspectorOf(ClassContext classContext, EventGraph eventGraph)
    {
        EventGraphViewModel? inspector = classContext.EventGraphs.FirstOrDefault(item => ReferenceEquals(item.Graph, eventGraph));
        inspector?.RefreshEntries();
        return inspector;
    }

    // Routes the context's inspector selections and class renames to the shell, once per context.
    private ClassContext Watch(ClassContext classContext)
    {
        if (watched.Add(classContext))
        {
            classContext.Messenger.Register<SelectInspectorMessage>(this);
            classContext.Messenger.Register<SelectEventEntryMessage>(this);
            classContext.ClassInspector.PropertyChanged += OnInspectorChanged;
            classContext.UndoRedo.Applied += OnUndoApplied;
        }

        return classContext;
    }

    private void Unwatch()
    {
        foreach (ClassContext classContext in watched)
        {
            classContext.ClassInspector.PropertyChanged -= OnInspectorChanged;
            classContext.UndoRedo.Applied -= OnUndoApplied;
            classContext.Messenger.UnregisterAll(this);
        }

        watched.Clear();
    }
}
