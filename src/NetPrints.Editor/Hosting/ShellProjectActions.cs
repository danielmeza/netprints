using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Main;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The project flows of the shell window: open, create, close and exit go to the project service
/// (<see cref="MainEditorViewModel"/>, which has no window of its own here); the member edits act on the class's
/// one editor and open the new graph as a document.
/// </summary>
/// <param name="projects">The project service.</param>
/// <param name="shell">The shell state.</param>
/// <param name="editorFor">Gets the editor of a class, the one the graph documents of the class are built on.</param>
internal sealed class ShellProjectActions(MainEditorViewModel projects, ShellViewModel shell, Func<ClassGraph, ClassEditorViewModel> editorFor) : IProjectActions
{
    private IProjectActions Flows => projects;

    /// <summary>Gets or sets the shell API; set once the layout exists.</summary>
    public IShell? Api { get; set; }

    /// <inheritdoc/>
    public Task<bool> ConfirmUnloadAsync(CancellationToken cancellationToken) => Flows.ConfirmUnloadAsync(cancellationToken);

    /// <inheritdoc/>
    public Task OpenProjectAsync(string? path, CancellationToken cancellationToken) => Flows.OpenProjectAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task NewProjectAsync(CancellationToken cancellationToken) => Flows.NewProjectAsync(cancellationToken);

    /// <inheritdoc/>
    public Task CloseProjectAsync(CancellationToken cancellationToken) => Flows.CloseProjectAsync(cancellationToken);

    /// <inheritdoc/>
    public Task ExitAsync(CancellationToken cancellationToken) => Flows.ExitAsync(cancellationToken);

    /// <inheritdoc/>
    public void ShowProjectSettings() => Api?.OpenDocument(DocumentId.ProjectSettings);

    /// <inheritdoc/>
    public Task ShowReferencesAsync(CancellationToken cancellationToken) => Flows.ShowReferencesAsync(cancellationToken);

    /// <inheritdoc/>
    public void ShowClassSettings(ClassGraph cls) => Inspect(cls);

    /// <inheritdoc/>
    public void AddMethod(ClassGraph cls) => AddGraph(cls, editor => editor.CreateMethodCommand.Execute(null));

    /// <inheritdoc/>
    public void AddConstructor(ClassGraph cls) => AddGraph(cls, editor => editor.CreateConstructorCommand.Execute(null));

    /// <inheritdoc/>
    public void AddEventGraph(ClassGraph cls) => AddGraph(cls, editor => editor.CreateEventGraphCommand.Execute(null));

    /// <inheritdoc/>
    public void AddVariable(ClassGraph cls)
    {
        editorFor(cls).CreateVariableCommand.Execute(null);
        if (cls.Variables.LastOrDefault() is { } variable)
        {
            Inspect(variable);
        }
    }

    /// <inheritdoc/>
    public void RenameItem(object item)
    {
        if (item is ClassGraph or MethodGraph or ConstructorGraph or Variable or EventGraph)
        {
            Inspect(item);
        }
    }

    /// <inheritdoc/>
    public void DeleteItem(object item)
    {
        switch (item)
        {
            case ClassGraph cls:
                CloseDocumentsOf(cls);
                projects.Project?.Classes.Remove(cls);
                break;
            case MethodGraph { Class: { } owner } method
                when editorFor(owner).Methods.FirstOrDefault(entry => ReferenceEquals(entry.Graph, method)) is { } methodEntry:
                CloseGraph(method);
                editorFor(owner).RemoveMethodCommand.Execute(methodEntry);
                break;
            case ConstructorGraph { Class: { } owner } constructor
                when editorFor(owner).Constructors.FirstOrDefault(entry => ReferenceEquals(entry.Graph, constructor)) is { } constructorEntry:
                CloseGraph(constructor);
                editorFor(owner).RemoveMethodCommand.Execute(constructorEntry);
                break;
            case Variable { Class: { } owner } variable
                when editorFor(owner).Variables.FirstOrDefault(entry => ReferenceEquals(entry.Variable, variable)) is { } variableEntry:
                variableEntry.RemoveCommand.Execute(null);
                break;
            case EventGraph { Class: { } owner } eventGraph
                when editorFor(owner).EventGraphs.FirstOrDefault(entry => ReferenceEquals(entry.Graph, eventGraph)) is { } eventEntry:
                CloseGraph(eventGraph);
                editorFor(owner).RemoveEventGraphCommand.Execute(eventEntry);
                break;
        }
    }

    /// <summary>Opens the class's graph, or the graph the given node lives in, and reveals the node.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="nodeId">The node to reveal, or null to open the class graph.</param>
    public void Navigate(ClassGraph cls, string? nodeId)
    {
        if (shell.Session is not { } session || Api is not { } api)
        {
            return;
        }

        NodeGraph target = nodeId is not null && GraphKeys.ForNode(cls, nodeId) is { } key && GraphKeys.Resolve(cls, key) is { } graph ? graph : cls;
        if (CommandTargets.GraphDocumentOf(session, target) is not { } id)
        {
            return;
        }

        api.OpenDocument(id);
        if (nodeId is not null && shell.FindDocument(id) is GraphDocumentViewModel document)
        {
            document.Graph.RevealNode(nodeId);
        }
    }

    private void Inspect(object item)
    {
        shell.TreeSelection = item;
        Api?.ShowPanel(PanelContributions.InspectorId);
    }

    private void AddGraph(ClassGraph cls, Action<ClassEditorViewModel> create)
    {
        ClassEditorViewModel editor = editorFor(cls);
        create(editor);
        if (shell.Session is { } session && editor.OpenedGraph is { Graph: { } graph } && CommandTargets.GraphDocumentOf(session, graph) is { } id)
        {
            Api?.OpenDocument(id);
            shell.TreeSelection = graph;
        }
    }

    private void CloseGraph(NodeGraph graph)
    {
        if (shell.Session is { } session && CommandTargets.GraphDocumentOf(session, graph) is { } id)
        {
            Api?.CloseDocument(id);
        }
    }

    private void CloseDocumentsOf(ClassGraph cls)
    {
        if (shell.Session is not { } session || Api is not { } api)
        {
            return;
        }

        string classPath = session.ClassPathOf(cls);
        foreach (DocumentId id in api.OpenDocuments.Where(id => id.ClassPath == classPath))
        {
            api.CloseDocument(id);
        }
    }
}
