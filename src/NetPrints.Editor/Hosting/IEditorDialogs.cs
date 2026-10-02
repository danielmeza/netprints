using NetPrints.Compilation;
using NetPrints.Core;
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

    /// <summary>Shows a list of diagnostics until the dialog is closed.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="issues">The diagnostics, one row each.</param>
    Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues);
}
