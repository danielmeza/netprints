using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.References;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Modal dialogs used by view models.
/// </summary>
public interface IEditorDialogs
{
    /// <summary>Shows an error with a copy-friendly message.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Lets the user choose a type; returns null when cancelled.</summary>
    Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial);

    /// <summary>Lets the user choose a method (the first one is preselected); returns null when cancelled.</summary>
    Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods);

    /// <summary>Shows the references dialog of a project until it is closed.</summary>
    Task ShowReferencesAsync(ReferenceListViewModel references);

    /// <summary>
    /// Asks whether a project's extensions may be loaded (extension-points.md §8.3): they run code in the
    /// editor's process. The answer is not remembered here; the caller records "Trust".
    /// </summary>
    /// <param name="projectPath">Full path of the project.</param>
    /// <param name="extensionFolders">Full paths of the project's <c>NetPrintsExtension</c> folders.</param>
    /// <returns><see langword="true"/> to trust the project, <see langword="false"/> to open it without them.</returns>
    Task<bool> ConfirmTrustAsync(string projectPath, IReadOnlyList<string> extensionFolders);

    /// <summary>Shows the keyboard shortcuts sheet until it is closed.</summary>
    /// <param name="sheet">The commands and their shortcuts.</param>
    Task ShowKeyboardShortcutsAsync(KeyboardShortcutsViewModel sheet);

    /// <summary>Shows the About dialog until it is closed.</summary>
    /// <param name="about">The version and the project links.</param>
    Task ShowAboutAsync(AboutViewModel about);

    /// <summary>Asks the user to confirm an action that cannot be undone.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">What will happen.</param>
    /// <param name="confirmLabel">The text of the button that goes ahead.</param>
    /// <returns><see langword="true"/> when the user confirmed, <see langword="false"/> when cancelled.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);

    /// <summary>Shows a list of diagnostics until the dialog is closed.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="issues">The diagnostics, one row each.</param>
    Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues);

    /// <summary>Asks what to do with the unsaved files before the project is unloaded.</summary>
    /// <param name="files">The unsaved files, listed in the dialog.</param>
    /// <returns>The user's choice; <see cref="UnloadChoice.Cancel"/> when the dialog was dismissed.</returns>
    Task<UnloadChoice> ConfirmUnsavedAsync(IReadOnlyList<UnsavedFile> files);

    /// <summary>Offers to restore the backed-up files of the project being opened.</summary>
    /// <param name="files">The backed-up files, listed in the dialog.</param>
    /// <returns>The user's answer, one restore-or-discard choice per file; <see cref="RecoveryAnswer.Later"/> when the dialog was dismissed.</returns>
    Task<RecoveryAnswer> ConfirmRecoverAsync(IReadOnlyList<RecoveryFile> files);
}
