using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Navigation;

/// <summary>
/// The go-to providers over the open project (FR-061): graphs, nodes, variables and methods. Each reads the session when it
/// is asked, so a provider registered once serves whichever project is open and finds nothing while none is.
/// </summary>
/// <param name="kind">One of the <see cref="GoToKinds"/> project kinds.</param>
/// <param name="session">Gets the open session, or null.</param>
public sealed class ProjectGoToProvider(string kind, Func<ProjectSessionViewModel?> session) : IGoToProvider
{
    private const int MaxNodes = 100;

    /// <inheritdoc/>
    public string Kind { get; } = kind;

    /// <inheritdoc/>
    public IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        return session() is { } open ? Items(open, text.Trim(), cancellationToken).ToAsyncEnumerable() : AsyncEnumerable.Empty<GoToItem>();
    }

    private IEnumerable<GoToItem> Items(ProjectSessionViewModel open, string text, CancellationToken cancellationToken)
    {
        ClassGraph[] classes = [.. open.Project.Classes];
        IEnumerable<GoToItem?> all = Kind switch
        {
            GoToKinds.Graphs => classes.SelectMany(cls => GraphsOf(cls)).Select(graph => ForGraph(open, graph)),
            GoToKinds.Methods => classes.SelectMany(cls => cls.Methods).Select(graph => ForGraph(open, graph)),
            GoToKinds.Variables => classes.SelectMany(cls => cls.Variables.Select(variable => ForVariable(open, cls, variable))),
            GoToKinds.Nodes when text.Length > 0 => classes.SelectMany(cls => AllGraphs(cls).SelectMany(graph => graph.Nodes.Select(node => ForNode(open, graph, node)))),
            _ => [],
        };
        List<GoToItem> matches = [];
        foreach (GoToItem item in all.OfType<GoToItem>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MatchRanking.Rank(item.Title, text) != MatchRanking.NoMatch)
            {
                matches.Add(item);
            }
        }

        if (Kind != GoToKinds.Nodes)
        {
            return matches;
        }

        return matches
            .OrderBy(item => MatchRanking.SortKey(item.Title, text))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(MaxNodes);
    }

    private static IEnumerable<NodeGraph> GraphsOf(ClassGraph cls) => new NodeGraph[] { cls }.Concat(cls.Constructors).Concat(cls.EventGraphs);

    private static IEnumerable<NodeGraph> AllGraphs(ClassGraph cls) => GraphsOf(cls).Concat(cls.Methods);

    private GoToItem? ForGraph(ProjectSessionViewModel open, NodeGraph graph) =>
        CommandTargets.GraphDocumentOf(open, graph) is { } id
            ? new GoToItem(Kind, GraphNames.Of(graph), CommandTargets.ClassOf(graph)?.FullName ?? "", new NavigationTarget(id))
            : null;

    private GoToItem? ForNode(ProjectSessionViewModel open, NodeGraph graph, Node node) =>
        CommandTargets.GraphDocumentOf(open, graph) is { } id
            ? new GoToItem(Kind, node.ToString(), (CommandTargets.ClassOf(graph)?.Name ?? "") + " › " + GraphNames.Of(graph), new NavigationTarget(id, node.Id))
            : null;

    private GoToItem? ForVariable(ProjectSessionViewModel open, ClassGraph cls, Variable variable)
    {
        object owner = (object?)variable.GetterMethod ?? cls;
        return CommandTargets.GraphDocumentOf(open, owner) is { } id ? new GoToItem(Kind, variable.Name, cls.FullName, new NavigationTarget(id)) : null;
    }
}
