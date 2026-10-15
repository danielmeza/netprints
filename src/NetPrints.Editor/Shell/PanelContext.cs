using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Shell;

/// <summary>What a tool panel's view model needs from the shell once the layout and the commands exist.</summary>
/// <param name="Shell">The shell state: the session, the active document and the tree selection.</param>
/// <param name="Api">The shell API: documents and panels.</param>
/// <param name="Commands">Runs and queries the registered commands.</param>
/// <param name="Context">Host services shared across the editor.</param>
public sealed record PanelContext(ShellViewModel Shell, IShell Api, CommandInvoker Commands, EditorContext Context);
