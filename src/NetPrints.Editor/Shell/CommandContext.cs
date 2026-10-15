using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>
/// Everything a command handler may look at, built per invocation by an <see cref="ICommandContextProvider"/>.
/// Holds no UI types.
/// </summary>
/// <param name="Shell">The shell services.</param>
/// <param name="Session">The open project session, or null while the start page shows.</param>
/// <param name="ActiveDocument">The active document, or null.</param>
/// <param name="ActiveGraph">The graph view model of the active graph document, or null.</param>
/// <param name="Selection">The current selection.</param>
/// <param name="Parameter">An optional command parameter, such as the id of a recent project.</param>
/// <param name="Scope">The scope the invocation comes from: <see cref="CommandScope.Graph"/> for a key pressed in the canvas, <see cref="CommandScope.ProjectTree"/> for the tree, <see cref="CommandScope.Global"/> when no surface is known (a menu, a bar button).</param>
public sealed record CommandContext(
    IShell Shell,
    ProjectSessionViewModel? Session,
    DocumentId? ActiveDocument,
    NodeGraphViewModel? ActiveGraph,
    CommandSelection Selection,
    object? Parameter = null,
    CommandScope Scope = CommandScope.Global);
