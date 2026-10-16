using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Shell;

/// <summary>
/// Everything a command handler may look at, built per invocation by an <see cref="ICommandContextProvider"/>.
/// Holds no UI types. The editor's own handlers read the view-model members; an extension reads the typed
/// <see cref="ICommandContext"/> members, which wrap them.
/// </summary>
/// <param name="Shell">The shell services.</param>
/// <param name="Session">The open project session, or null while the start page shows.</param>
/// <param name="ActiveDocument">The active document, or null.</param>
/// <param name="ActiveGraph">The graph view model of the active graph document, or null.</param>
/// <param name="Selection">The current selection.</param>
/// <param name="Parameter">An optional command parameter, such as the id of a recent project.</param>
/// <param name="Scope">The scope the invocation comes from: <see cref="CommandScope.Graph"/> for a key pressed in the canvas, <see cref="CommandScope.ProjectTree"/> for the tree, <see cref="CommandScope.Global"/> when no surface is known (a menu, a bar button).</param>
/// <param name="Host">The editor services the typed dialogs and output wrap; set by the shell's provider.</param>
public sealed record CommandContext(
    IShell Shell,
    ProjectSessionViewModel? Session,
    DocumentId? ActiveDocument,
    NodeGraphViewModel? ActiveGraph,
    CommandSelection Selection,
    object? Parameter = null,
    CommandScope Scope = CommandScope.Global,
    EditorContext? Host = null) : ICommandContext
{
    /// <inheritdoc/>
    IEditorShell ICommandContext.Shell => new EditorShellFacade(() => Shell);

    /// <inheritdoc/>
    IProjectSession? ICommandContext.Session => Session is { } session && Host is { } host ? new ProjectSessionFacade(session, host, ActiveGraph) : null;

    /// <inheritdoc/>
    IGraphEditor? ICommandContext.ActiveGraph => ActiveGraph is { } graph ? new GraphEditorFacade(graph) : null;

    /// <inheritdoc/>
    IDialogService ICommandContext.Dialogs => new EditorDialogService(RequireHost().Dialogs);

    /// <inheritdoc/>
    IEditorOutput ICommandContext.Output => new EditorOutput(RequireHost().RunState);

    /// <inheritdoc/>
    IServiceProvider ICommandContext.Services => EmptyServiceProvider.Instance;

    /// <summary>Gets the editor's own context behind the typed one.</summary>
    /// <param name="context">The context a handler was given.</param>
    /// <returns>The context the editor built.</returns>
    /// <exception cref="InvalidOperationException">The context was built by something other than the editor's shell.</exception>
    public static CommandContext From(ICommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context as CommandContext ?? throw new InvalidOperationException("The editor's own commands run only in the editor's shell.");
    }

    private EditorContext RequireHost() =>
        Host ?? throw new InvalidOperationException("This command context was built without the editor's services.");
}
