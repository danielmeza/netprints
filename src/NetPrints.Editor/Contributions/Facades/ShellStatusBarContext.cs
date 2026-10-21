using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Contributions;

/// <summary>What a status bar item of one extension may look at: the shell's open session and active graph, and the extension's own services.</summary>
/// <param name="source">The shell the bar belongs to.</param>
/// <param name="shell">The shell as an extension sees it.</param>
/// <param name="host">The editor services, or null when the shell was built without them.</param>
/// <param name="owner">The extension that owns the item, or null for none.</param>
internal sealed class ShellStatusBarContext(ShellViewModel source, IEditorShell shell, EditorContext? host, string? owner) : IStatusBarContext
{
    /// <summary>Gets the shell the bar belongs to, for the editor's own segments.</summary>
    public ShellViewModel Source => source;

    /// <inheritdoc/>
    public IEditorShell Shell => shell;

    /// <inheritdoc/>
    public IProjectSession? Session => source.Session is { } session && host is { } services
        ? new ProjectSessionFacade(session, services, (source.ActiveDocument as GraphDocumentViewModel)?.Graph, owner)
        : null;

    /// <inheritdoc/>
    public IGraphEditor? ActiveGraph => (source.ActiveDocument as GraphDocumentViewModel)?.Graph is { } graph ? new GraphEditorFacade(graph) : null;

    /// <inheritdoc/>
    public IServiceProvider Services => owner is not null && host is { } services ? services.Extensions.Current.ServicesOf(owner) : EmptyServiceProvider.Instance;
}
