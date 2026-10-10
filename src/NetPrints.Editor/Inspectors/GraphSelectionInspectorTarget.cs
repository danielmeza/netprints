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

        return graph.Class?.Project is { } project
            ? node switch
            {
                VariableNode { IsLocalVariable: false } variable => FindVariable(project, variable.Variable),
                CallMethodNode call => FindMethod(project, call.MethodSpecifier),
                _ => null,
            }
            : null;
    }

    private static Variable? FindVariable(Project project, VariableSpecifier specifier) =>
        ClassNamed(project, specifier.DeclaringType)?.Variables.FirstOrDefault(variable => string.Equals(variable.Name, specifier.Name, StringComparison.Ordinal));

    private static MethodGraph? FindMethod(Project project, MethodSpecifier specifier) =>
        ClassNamed(project, specifier.DeclaringType)?.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, specifier.Name, StringComparison.Ordinal)
            && method.NamedArgumentTypes.Select(argument => argument.Value.FullCodeName).SequenceEqual(specifier.ArgumentTypes.Select(argument => argument.FullCodeName), StringComparer.Ordinal));

    private static ClassGraph? ClassNamed(Project project, TypeSpecifier? type) =>
        type is null ? null : project.Classes.FirstOrDefault(cls => string.Equals(cls.FullName, type.Name, StringComparison.Ordinal));

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
