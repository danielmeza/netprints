using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>
/// Builds the command context of the shell window: the open session, the active document and, when it is a graph, the
/// graph and its selected nodes. Raises <see cref="CommandStatesChanged"/> when the shell does (the session is replaced,
/// the session builds, runs or changes an undo history, the active document or the project tree's selection changes) and when the active graph's
/// selection does; it watches only while something listens.
/// </summary>
/// <param name="shell">The shell state.</param>
/// <param name="services">The shell services of the context.</param>
public sealed class ShellCommandContextProvider(ShellViewModel shell, IShell services) : ICommandContextProvider
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
    public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global)
    {
        NodeGraphViewModel? graph = ActiveGraph();
        var selection = new CommandSelection(graph is null ? [] : [.. graph.SelectedNodes], shell.TreeSelection);
        return new CommandContext(services, shell.Session, shell.ActiveDocument?.Id, graph, selection, parameter, scope);
    }

    private NodeGraphViewModel? ActiveGraph() => (shell.ActiveDocument as GraphDocumentViewModel)?.Graph;

    private void Watch()
    {
        shell.CommandStatesChanged += OnShellPulse;
        WatchGraph(ActiveGraph());
    }

    private void Unwatch()
    {
        shell.CommandStatesChanged -= OnShellPulse;
        WatchGraph(null);
    }

    private void WatchGraph(NodeGraphViewModel? graph)
    {
        if (ReferenceEquals(watchedGraph, graph))
        {
            return;
        }

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

    private void OnShellPulse(object? sender, EventArgs e)
    {
        WatchGraph(ActiveGraph());
        OnPulse(sender, e);
    }

    private void OnPulse(object? sender, EventArgs e) => handlers?.Invoke(this, EventArgs.Empty);
}
