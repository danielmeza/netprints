using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Shell;

/// <summary>
/// Everything a command handler may look at, built per invocation by an <see cref="ICommandContextProvider"/>.
/// Holds no UI types.
/// </summary>
/// <param name="Shell">The shell services.</param>
/// <param name="Session">The open project session, or null while the start page shows. Typed <see cref="object"/> until the session view model exists (sub-phase C).</param>
/// <param name="ActiveDocument">The active document, or null.</param>
/// <param name="ActiveGraph">The graph view model of the active graph document, or null.</param>
/// <param name="Selection">The current selection.</param>
/// <param name="Parameter">An optional command parameter, such as the id of a recent project.</param>
public sealed record CommandContext(
    IShell Shell,
    object? Session,
    DocumentId? ActiveDocument,
    NodeGraphViewModel? ActiveGraph,
    CommandSelection Selection,
    object? Parameter = null);
