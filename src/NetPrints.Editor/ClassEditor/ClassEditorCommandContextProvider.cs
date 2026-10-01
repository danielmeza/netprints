using System.ComponentModel;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// Builds the command context of a class editor window: its open graph, that graph's selected nodes and the project
/// session. Raises <see cref="CommandStatesChanged"/> when the session pulses, the open graph changes or its
/// selection does; it watches those only while something listens.
/// </summary>
/// <param name="editor">The class editor.</param>
/// <param name="session">Gets the open project session, or null.</param>
/// <param name="shell">The shell services.</param>
/// <param name="sessionStates">Raises when the session is replaced or its command state changes.</param>
internal sealed class ClassEditorCommandContextProvider(ClassEditorViewModel editor, Func<ProjectSessionViewModel?> session, IShell shell, ICommandStateSource sessionStates) : ICommandContextProvider
{
    private EventHandler? handlers;
    private NodeGraphViewModel? watchedGraph;

    /// <inheritdoc/>
    public event EventHandler? CommandStatesChanged
    {
        add
        {
            if (handlers is null)
            {
                Watch();
            }

            handlers += value;
        }

        remove
        {
            handlers -= value;
            if (handlers is null)
            {
                Unwatch();
            }
        }
    }

    /// <inheritdoc/>
    public CommandContext Create(object? parameter = null)
    {
        ProjectSessionViewModel? current = session();
        var graph = editor.OpenedGraph;
        DocumentId? document = current is null ? null : DocumentId.Graph(current.ClassPathOf(editor.Class), "class");
        var selection = new CommandSelection(graph is null ? [] : [.. graph.SelectedNodes]);
        return new CommandContext(shell, current, document, graph, selection, parameter);
    }

    private void Watch()
    {
        sessionStates.CommandStatesChanged += OnPulse;
        editor.PropertyChanged += OnEditorPropertyChanged;
        WatchGraph(editor.OpenedGraph);
    }

    private void Unwatch()
    {
        sessionStates.CommandStatesChanged -= OnPulse;
        editor.PropertyChanged -= OnEditorPropertyChanged;
        WatchGraph(null);
    }

    private void WatchGraph(NodeGraphViewModel? graph)
    {
        if (watchedGraph is not null)
        {
            watchedGraph.SelectionChanged -= OnPulse;
        }

        watchedGraph = graph;
        if (graph is not null)
        {
            graph.SelectionChanged += OnPulse;
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassEditorViewModel.OpenedGraph))
        {
            WatchGraph(editor.OpenedGraph);
            OnPulse(this, EventArgs.Empty);
        }
    }

    private void OnPulse(object? sender, EventArgs e) => handlers?.Invoke(this, EventArgs.Empty);
}
