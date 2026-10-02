using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Main;

/// <summary>
/// Opens a class editor window and wires its commands, as the former main window did. Nothing in the application opens
/// class windows any more; the headless suites of the class windows do, until those windows are removed.
/// </summary>
internal static class LegacyClassWindows
{
    /// <summary>Opens the window of a class, reusing an open one, and returns its editor.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="session">Gets the open project session, or null.</param>
    /// <param name="states">Raises when the session is replaced or its command state changes.</param>
    /// <param name="projectActions">The project flows the editor's shell exposes.</param>
    /// <param name="cls">The class.</param>
    /// <returns>The window's editor, or null when the window service has none for the class.</returns>
    public static ClassEditorViewModel? Open(EditorContext context, Func<ProjectSessionViewModel?> session, ICommandStateSource states, IProjectActions projectActions, ClassGraph cls)
    {
        if (context.Windows.TryActivateClassEditor(cls))
        {
            return context.Windows.FindClassEditor(cls);
        }

        context.Windows.OpenClassEditor(cls, context);
        if (context.Windows.FindClassEditor(cls) is not { } editor)
        {
            return null;
        }

        var registry = new ContributionRegistry(context.LoggerFactory.CreateLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        registry.Freeze();
        session()?.UseUndoStack(editor.Class, editor.UndoRedo);
        editor.SessionSource = session;
        editor.Commands = new CommandInvoker(
            registry,
            new ClassEditorCommandContextProvider(editor, session, new LegacyWindowShell(projectActions), states),
            exception => context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync("The command failed", exception.ToString()).Forget(context.LoggerFactory.CreateLogger("LegacyClassWindows"))));
        return editor;
    }
}
