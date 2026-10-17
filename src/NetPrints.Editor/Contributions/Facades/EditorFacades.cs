using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Contributions;

/// <summary>The services of an extension that has none: no service is ever found.</summary>
internal sealed class EmptyServiceProvider : IServiceProvider
{
    private EmptyServiceProvider()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static EmptyServiceProvider Instance { get; } = new();

    /// <inheritdoc/>
    public object? GetService(Type serviceType) => null;
}

/// <summary>The shell as an extension sees it: panels and the layout, over the shell services of the moment.</summary>
/// <param name="shell">Gets the shell services; asked on each call because the shell host composes them after the panels.</param>
internal sealed class EditorShellFacade(Func<Shell.IShell> shell) : IEditorShell
{
    /// <inheritdoc/>
    public bool IsPanelVisible(string panelId) => shell().IsPanelVisible(panelId);

    /// <inheritdoc/>
    public void ShowPanel(string panelId) => shell().ShowPanel(panelId);

    /// <inheritdoc/>
    public void HidePanel(string panelId) => shell().HidePanel(panelId);

    /// <inheritdoc/>
    public void ResetLayout() => shell().ResetLayout();
}

/// <summary>The dialogs of the editor as an extension sees them.</summary>
/// <param name="dialogs">The editor's dialogs.</param>
/// <param name="registry">The registry the dialogs shown by id are looked up in, or null for none.</param>
/// <param name="context">Creates the context a dialog's view model is created with, or null for none.</param>
internal sealed class EditorDialogService(IEditorDialogs dialogs, IContributionRegistry? registry = null, Func<IDialogContext>? context = null) : IDialogService
{
    /// <inheritdoc/>
    public async Task<object> ShowDialogAsync(string dialogId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialogId);
        DialogDescriptor dialog = registry?.Dialogs.FirstOrDefault(candidate => candidate.Id == dialogId)
            ?? throw new ArgumentException($"No dialog is registered with the id '{dialogId}'.", nameof(dialogId));
        IDialogContext dialogContext = context?.Invoke() ?? throw new InvalidOperationException("This dialog service was built without a context.");
        object viewModel = dialog.CreateViewModel(dialogContext);
        await dialogs.ShowViewModelAsync(dialog.DisplayTitle, viewModel, cancellationToken).ConfigureAwait(true);
        return viewModel;
    }

    /// <inheritdoc/>
    public void ShowNotification(string title, string message) => dialogs.ShowNotification(title, message);

    /// <inheritdoc/>
    public Task ShowErrorAsync(string title, string message) => dialogs.ShowErrorAsync(title, message);
}

/// <summary>The Output panel as an extension sees it.</summary>
/// <param name="tracker">The run state whose lines the Output panel shows.</param>
internal sealed class EditorOutput(RunStateTracker tracker) : IEditorOutput
{
    /// <inheritdoc/>
    public void WriteLine(string text) => tracker.WriteLine(text);
}

/// <summary>The undo history of a class as an extension sees it.</summary>
/// <param name="stack">The history.</param>
internal sealed class UndoFacade(UndoRedoStack stack) : IEditorUndo
{
    /// <inheritdoc/>
    public bool CanUndo => stack.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo => stack.CanRedo;

    /// <inheritdoc/>
    public string? UndoName => stack.UndoName;

    /// <inheritdoc/>
    public string? RedoName => stack.RedoName;

    /// <inheritdoc/>
    public bool Undo() => stack.Undo();

    /// <inheritdoc/>
    public bool Redo() => stack.Redo();
}

/// <summary>A graph being edited as an extension sees it.</summary>
/// <param name="graph">The graph view model.</param>
internal sealed class GraphEditorFacade(NodeGraphViewModel graph) : IGraphEditor
{
    /// <inheritdoc/>
    public NodeGraph Graph => graph.Graph;

    /// <inheritdoc/>
    public IReadOnlyList<Node> SelectedNodes => [.. graph.SelectedNodes.Select(node => node.Node)];

    /// <inheritdoc/>
    public IEditorUndo Undo => new UndoFacade(graph.Services.UndoRedo);

    /// <inheritdoc/>
    public Node Place(Func<NodeGraph, Node> create, GraphPoint position, NodePin? connectTo)
    {
        ArgumentNullException.ThrowIfNull(create);
        return graph.AddNode(position, connectTo, create);
    }
}

/// <summary>The open project as an extension sees it.</summary>
/// <param name="session">The session.</param>
/// <param name="host">The editor services, for the reflection provider.</param>
/// <param name="activeGraph">The graph being edited, or null.</param>
/// <param name="owner">The extension whose session-scoped services <see cref="Services"/> gives, or null for none.</param>
internal sealed class ProjectSessionFacade(Shell.ProjectSessionViewModel session, EditorContext host, NodeGraphViewModel? activeGraph, string? owner = null) : IProjectSession
{
    /// <inheritdoc/>
    public Project Project => session.Project;

    /// <inheritdoc/>
    public CSharpTarget Target => session.Project.CSharpTarget;

    /// <inheritdoc/>
    public IReflectionProvider Reflection => host.Reflection.Provider;

    /// <inheritdoc/>
    public IGraphEditor? ActiveGraph => activeGraph is null ? null : new GraphEditorFacade(activeGraph);

    /// <inheritdoc/>
    public IServiceProvider Services => owner is null ? EmptyServiceProvider.Instance : session.ExtensionServices(owner);
}
