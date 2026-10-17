using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Inspectors;

/// <summary>
/// Maps what is selected in a graph to the project member the inspector shows (FR-013, graph half): a variable getter or setter node
/// to its variable, a call to a method of a project class to that method, an event entry to itself, and anything else to the member that owns the graph.
/// </summary>
public static class GraphSelectionInspectorTarget
{
    /// <summary>Finds the member to inspect for a selection in <paramref name="graph"/>.</summary>
    /// <param name="graph">The graph the selection is in.</param>
    /// <param name="selectedNodes">The selected nodes; only a single selected node maps to a member of its own.</param>
    /// <returns>A <see cref="Variable"/>, an <see cref="EventEntryNode"/>, <see cref="MethodGraph"/>, <see cref="ConstructorGraph"/>, <see cref="ClassGraph"/> or other graph, as the project tree selects them, or null when the graph has no class.</returns>
    public static object? Resolve(NodeGraph graph, IReadOnlyList<Node> selectedNodes)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(selectedNodes);
        return (selectedNodes.Count == 1 ? MemberOf(graph, selectedNodes[0]) : null) ?? OwnerOf(graph);
    }

    private static object? MemberOf(NodeGraph graph, Node node)
    {
        if (node is EventEntryNode entry)
        {
            return entry;
        }

        return graph.Class?.Project is { } project && node is IMemberReferencingNode referencing
            ? FindMember(project, referencing)
            : null;
    }

    private static object? FindMember(Project project, IMemberReferencingNode referencing)
    {
        foreach (ClassGraph cls in project.Classes)
        {
            foreach (Variable variable in cls.Variables)
            {
                if (referencing.RefersTo(new MemberKey(MemberKind.Variable, cls.Type, variable.Name, [])))
                {
                    return variable;
                }
            }

            foreach (MethodGraph method in cls.Methods)
            {
                if (referencing.RefersTo(new MemberKey(MemberKind.Method, cls.Type, method.Name, [.. method.ArgumentTypes])))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static object OwnerOf(NodeGraph graph)
    {
        switch (graph)
        {
            case MethodGraph { Class: { } cls } method:
                return cls.Variables.FirstOrDefault(variable => ReferenceEquals(variable.GetterMethod, method) || ReferenceEquals(variable.SetterMethod, method)) ?? (object)method;
            case TypeGraph { OwningClass: { } owner } typeGraph:
                return owner.Variables.FirstOrDefault(variable => ReferenceEquals(variable.TypeGraph, typeGraph)) ?? (object)typeGraph;
            default:
                return graph;
        }
    }
}
