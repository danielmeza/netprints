using NetPrints.Core;

namespace NetPrints.Editor.Shell;

/// <summary>
/// The project-level flows the project commands wrap: opening, creating and closing a project, the settings and
/// references dialogs, adding classes, and the member and tree-item edits.
/// </summary>
public interface IProjectActions
{
    /// <summary>
    /// Asks whether the open project may be unloaded. The one place the unsaved changes prompt goes, with two callers:
    /// <c>UnloadingCommandHandler</c> (open another, create, close) and the window-close path (exit and the OS close).
    /// </summary>
    /// <param name="cancellationToken">Cancels the prompt.</param>
    /// <returns><see langword="true"/> to go on, <see langword="false"/> when the user kept the project.</returns>
    Task<bool> ConfirmUnloadAsync(CancellationToken cancellationToken);

    /// <summary>Opens a project.</summary>
    /// <param name="path">The <c>.csproj</c> path, or null to ask the user with a file picker.</param>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the project is open, or the user cancelled.</returns>
    Task OpenProjectAsync(string? path, CancellationToken cancellationToken);

    /// <summary>Asks for a folder and name, creates a project there and opens it.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the project is open, or the user cancelled.</returns>
    Task NewProjectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the open project and its documents.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the project is closed.</returns>
    Task CloseProjectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the main window, which ends the application; it asks nothing, the window-close path owns the unload prompt.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the window was asked to close.</returns>
    Task ExitAsync(CancellationToken cancellationToken);

    /// <summary>Shows the project settings.</summary>
    void ShowProjectSettings();

    /// <summary>Shows the References dialog of the open project.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowReferencesAsync(CancellationToken cancellationToken);

    /// <summary>Adds a uniquely named class to the open project.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the class was added or the failure shown.</returns>
    Task NewClassAsync(CancellationToken cancellationToken);

    /// <summary>Asks for a <c>*.netpc.json</c> class file and copies it into the open project.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>A task that completes when the class was added, or the user cancelled.</returns>
    Task AddExistingClassAsync(CancellationToken cancellationToken);

    /// <summary>Shows the settings (inspector) of a class.</summary>
    /// <param name="cls">The class.</param>
    void ShowClassSettings(ClassGraph cls);

    /// <summary>Adds a method to a class and opens it.</summary>
    /// <param name="cls">The class.</param>
    void AddMethod(ClassGraph cls);

    /// <summary>Adds a constructor to a class and opens it.</summary>
    /// <param name="cls">The class.</param>
    void AddConstructor(ClassGraph cls);

    /// <summary>Adds a variable to a class.</summary>
    /// <param name="cls">The class.</param>
    void AddVariable(ClassGraph cls);

    /// <summary>Adds an event graph to a class and opens it.</summary>
    /// <param name="cls">The class.</param>
    void AddEventGraph(ClassGraph cls);

    /// <summary>Starts renaming a class, member or graph.</summary>
    /// <param name="item">The project tree item, or the graph model of the active document.</param>
    void RenameItem(object item);

    /// <summary>Deletes a class or member after the user confirmed.</summary>
    /// <param name="item">The project tree item.</param>
    void DeleteItem(object item);
}
