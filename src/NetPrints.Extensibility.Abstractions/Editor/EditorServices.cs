using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>The part of the editor shell an extension may drive: panels and the layout.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IEditorShell
{
    /// <summary>Gets whether a panel is shown: in the layout, docked or floating, and not hidden.</summary>
    /// <param name="panelId">The panel id.</param>
    /// <returns><see langword="true"/> when the panel is shown.</returns>
    bool IsPanelVisible(string panelId);

    /// <summary>Shows a panel.</summary>
    /// <param name="panelId">The panel id.</param>
    void ShowPanel(string panelId);

    /// <summary>Hides a panel.</summary>
    /// <param name="panelId">The panel id.</param>
    void HidePanel(string panelId);

    /// <summary>Restores the default layout.</summary>
    void ResetLayout();
}

/// <summary>The dialogs an extension may show.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IDialogService
{
    /// <summary>Shows a non-blocking toast notification.</summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    void ShowNotification(string title, string message);

    /// <summary>Shows an error with a copy-friendly message.</summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Shows a dialog registered with <see cref="IEditorContributions.AddDialog"/>: its factory creates the view model with an <see cref="IDialogContext"/>, and the editor shows it in a window.</summary>
    /// <param name="dialogId">The id of the dialog.</param>
    /// <param name="cancellationToken">Cancels waiting for the dialog; the window is closed.</param>
    /// <returns>A task that completes when the dialog is closed, with the view model it showed, which holds the dialog's result.</returns>
    /// <exception cref="ArgumentException">No dialog is registered with <paramref name="dialogId"/>.</exception>
    Task<object> ShowDialogAsync(string dialogId, CancellationToken cancellationToken = default);
}

/// <summary>The Output panel: where an extension writes lines for the user.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IEditorOutput
{
    /// <summary>Writes a line.</summary>
    /// <param name="text">The line, without its line ending.</param>
    void WriteLine(string text);
}

/// <summary>The undo history of the graph being edited.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IEditorUndo
{
    /// <summary>Gets whether there is a step to undo.</summary>
    bool CanUndo { get; }

    /// <summary>Gets whether there is a step to redo.</summary>
    bool CanRedo { get; }

    /// <summary>Gets the name of the step <see cref="Undo"/> would take back, or <see langword="null"/>.</summary>
    string? UndoName { get; }

    /// <summary>Gets the name of the step <see cref="Redo"/> would apply again, or <see langword="null"/>.</summary>
    string? RedoName { get; }

    /// <summary>Takes back the last step.</summary>
    /// <returns><see langword="true"/> when a step was undone.</returns>
    bool Undo();

    /// <summary>Applies the last undone step again.</summary>
    /// <returns><see langword="true"/> when a step was redone.</returns>
    bool Redo();
}
