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

    /// <summary>The document of the selected project tree item when it is a graph: the class graph, a method, a constructor or an event graph.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>The document id, or null when there is no session or the selected item is not a graph.</returns>
    public static DocumentId? TreeSelectionDocument(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Session is { } session ? GraphDocumentOf(session, context.Selection.TreeItem) : null;
    }

    /// <summary>The document id of a graph of the open project.</summary>
    /// <param name="session">The open project session.</param>
    /// <param name="item">A class, method, constructor, event graph, or a variable's getter, setter or type graph.</param>
    /// <returns>The document id, or null when <paramref name="item"/> is none of those or has no class.</returns>
    public static DocumentId? GraphDocumentOf(ProjectSessionViewModel session, object? item)
    {
        ArgumentNullException.ThrowIfNull(session);
        (ClassGraph? owner, string? key) = item switch
        {
            ClassGraph cls => (cls, DocumentId.ClassGraphKey),
            MethodGraph method => (method.Class, AccessorKey(method) ?? DocumentId.MethodKeyPrefix + method.Id),
            ConstructorGraph constructor => (constructor.Class, DocumentId.ConstructorKeyPrefix + constructor.Id),
            EventGraph eventGraph => (eventGraph.Class, DocumentId.EventKeyPrefix + eventGraph.Id),
            TypeGraph typeGraph => (typeGraph.OwningClass, typeGraph.OwningClass?.Variables.FirstOrDefault(v => ReferenceEquals(v.TypeGraph, typeGraph)) is { } owning ? DocumentId.TypeKeyPrefix + owning.Id : null),
            _ => (null, null),
        };
        return owner is not null && key is not null ? DocumentId.Graph(session.ClassPathOf(owner), key) : null;
    }

    /// <summary>The class that owns a graph.</summary>
    /// <param name="graph">Any graph, including a variable's type graph.</param>
    /// <returns>The class, or null when the graph has none.</returns>
    public static ClassGraph? ClassOf(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph switch
        {
            ClassGraph cls => cls,
            TypeGraph typeGraph => typeGraph.OwningClass,
            _ => graph.Class,
        };
    }

    private static string? AccessorKey(MethodGraph method)
    {
        foreach (Variable variable in method.Class?.Variables ?? [])
        {
            if (ReferenceEquals(variable.GetterMethod, method))
            {
                return DocumentId.GetterKeyPrefix + variable.Id;
            }

            if (ReferenceEquals(variable.SetterMethod, method))
            {
                return DocumentId.SetterKeyPrefix + variable.Id;
            }
        }

        return null;
    }

    /// <summary>The graph a document shows.</summary>
    /// <param name="session">The open project session.</param>
    /// <param name="id">A document id.</param>
    /// <returns>The class, method, constructor, event, getter, setter or type graph, or null when <paramref name="id"/> is not a graph document of the project.</returns>
    public static NodeGraph? GraphOf(ProjectSessionViewModel session, DocumentId id)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(id);
        if (id is not { Kind: DocumentKind.Graph, ClassPath: { } classPath, GraphKey: { } key } || session.FindClass(classPath) is not { } cls)
        {
            return null;
        }

        return key switch
        {
            DocumentId.ClassGraphKey => cls,
            _ when key.StartsWith(DocumentId.MethodKeyPrefix, StringComparison.Ordinal) => cls.Methods.FirstOrDefault(m => m.Id == key[DocumentId.MethodKeyPrefix.Length..]),
            _ when key.StartsWith(DocumentId.ConstructorKeyPrefix, StringComparison.Ordinal) => cls.Constructors.FirstOrDefault(c => c.Id == key[DocumentId.ConstructorKeyPrefix.Length..]),
            _ when key.StartsWith(DocumentId.EventKeyPrefix, StringComparison.Ordinal) => cls.EventGraphs.FirstOrDefault(g => g.Id == key[DocumentId.EventKeyPrefix.Length..]),
            _ when key.StartsWith(DocumentId.GetterKeyPrefix, StringComparison.Ordinal) => VariableOf(cls, key, DocumentId.GetterKeyPrefix)?.GetterMethod,
            _ when key.StartsWith(DocumentId.SetterKeyPrefix, StringComparison.Ordinal) => VariableOf(cls, key, DocumentId.SetterKeyPrefix)?.SetterMethod,
            _ when key.StartsWith(DocumentId.TypeKeyPrefix, StringComparison.Ordinal) => VariableOf(cls, key, DocumentId.TypeKeyPrefix)?.TypeGraph,
            _ => null,
        };
    }

    private static Variable? VariableOf(ClassGraph cls, string key, string prefix) => cls.Variables.FirstOrDefault(v => v.Id == key[prefix.Length..]);
}
