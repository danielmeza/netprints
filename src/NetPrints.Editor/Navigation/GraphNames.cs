using NetPrints.Core;

namespace NetPrints.Editor.Navigation;

/// <summary>The names go to anything shows for the graphs of a project.</summary>
internal static class GraphNames
{
    /// <summary>The text shown for a graph: the class, method or event graph name, or the class name followed by <c>()</c> for a constructor.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The display name.</returns>
    public static string Of(NodeGraph graph) => graph switch
    {
        ClassGraph cls => cls.Name,
        MethodGraph method => method.Name,
        EventGraph events => events.Name,
        ConstructorGraph constructor => (constructor.Class?.Name ?? "") + "()",
        _ => graph.ToString() ?? "",
    };
}
