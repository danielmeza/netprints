using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>Finds the class a command acts on from a <see cref="CommandContext"/>.</summary>
public static class CommandTargets
{
    /// <summary>The class of the active graph document.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>The class, or null when no project is open or the active document is not a graph of one of its classes.</returns>
    public static ClassGraph? ActiveDocumentClass(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Session is { } session && context.ActiveDocument is { ClassPath: { } classPath }
            ? session.FindClass(classPath)
            : null;
    }

    /// <summary>The class of the selected project tree item: the class itself, or the owner of a graph or variable.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>The class, or null when nothing is selected in the tree or the item has no class.</returns>
    public static ClassGraph? TreeSelectionClass(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Selection.TreeItem switch
        {
            ClassGraph cls => cls,
            NodeGraph graph => graph.Class,
            Variable variable => variable.Class,
            _ => null,
        };
    }
}
