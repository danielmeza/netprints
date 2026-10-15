using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Shell;

/// <summary>Creates graph documents that own the graph editor view model they show.</summary>
public static class GraphDocumentFactory
{
    /// <summary>
    /// Opens the graph <paramref name="model"/> as the document <paramref name="id"/>, with a graph editor view model of its
    /// own that is disposed when the document is: until then it stays subscribed to the reflection host.
    /// </summary>
    /// <param name="id">The graph document id.</param>
    /// <param name="model">The graph to edit.</param>
    /// <param name="services">The services of the class that owns the graph.</param>
    /// <param name="owner">The class that owns the graph; its file is the one that can be unsaved.</param>
    /// <param name="session">The session whose pulses refresh the unsaved state, or null.</param>
    /// <param name="invoker">The invoker the canvas runs its key bindings through, or null.</param>
    /// <param name="breadcrumbs">Creates the breadcrumbs shown above the canvas from the graph view model, or null for none.</param>
    /// <returns>The document.</returns>
    public static GraphDocumentViewModel Open(DocumentId id, NodeGraph model, ClassEditorServices services, ClassGraph owner, ProjectSessionViewModel? session, CommandInvoker? invoker, Func<NodeGraphViewModel, BreadcrumbsViewModel?>? breadcrumbs = null)
    {
        var graph = new NodeGraphViewModel(model, services);
        BreadcrumbsViewModel? crumbs = breadcrumbs?.Invoke(graph);
        var document = new GraphDocumentViewModel(id, graph, owner, session) { Invoker = invoker, Breadcrumbs = crumbs };
        document.Disposed += (_, _) =>
        {
            crumbs?.Dispose();
            graph.Dispose();
        };
        return document;
    }
}
