using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ClassEditor;

/// <summary>Builds the command context of a class editor window: its open graph, that graph's selected nodes and the project session.</summary>
/// <param name="editor">The class editor.</param>
/// <param name="session">Gets the open project session, or null.</param>
/// <param name="shell">The shell services.</param>
internal sealed class ClassEditorCommandContextProvider(ClassEditorViewModel editor, Func<ProjectSessionViewModel?> session, IShell shell) : ICommandContextProvider
{
    /// <inheritdoc/>
    public CommandContext Create(object? parameter = null)
    {
        ProjectSessionViewModel? current = session();
        var graph = editor.OpenedGraph;
        DocumentId? document = current is null ? null : DocumentId.Graph(current.ClassPathOf(editor.Class), "class");
        var selection = new CommandSelection(graph is null ? [] : [.. graph.SelectedNodes]);
        return new CommandContext(shell, current, document, graph, selection, parameter);
    }
}
