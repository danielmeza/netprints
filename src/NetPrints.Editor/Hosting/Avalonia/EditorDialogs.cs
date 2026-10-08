using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.References;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Modal Avalonia dialogs owned by the active window.</summary>
public sealed class EditorDialogs(Func<Window?> owner) : IEditorDialogs
{
    private const int MaxNotifications = 3;
    private WindowNotificationManager? notificationManager;

    /// <inheritdoc/>
    public void ShowNotification(string title, string message)
    {
        if (owner() is { } window)
        {
            notificationManager ??= new WindowNotificationManager(window)
            {
                Position = NotificationPosition.BottomRight,
                MaxItems = MaxNotifications
            };
            notificationManager.Show(new Notification(title, message, NotificationType.Information));
        }
    }
    private async Task<T?> ShowAsync<T>(Window dialog)
    {
        if (owner() is { } parent)
        {
            return await dialog.ShowDialog<T?>(parent);
        }

        // No owner (e.g. before the main window exists): show modeless and wait for close.
        var closed = new TaskCompletionSource<T?>();
        dialog.Closed += (_, _) => closed.TrySetResult(dialog is IDialogResult<T> result ? result.Result : default);
        dialog.Show();
        return await closed.Task;
    }

    /// <inheritdoc/>
    public Task ShowErrorAsync(string title, string message) =>
        ShowAsync<object>(new ErrorDialog(title, message));

    /// <inheritdoc/>
    public Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial) =>
        ShowAsync<TypeSpecifier>(new SelectTypeDialog(types, initial));

    /// <inheritdoc/>
    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods) =>
        ShowAsync<MethodSpecifier>(new SelectMethodDialog(methods));

    /// <inheritdoc/>
    public Task<bool> ConfirmTrustAsync(string projectPath, IReadOnlyList<string> extensionFolders) =>
        ShowAsync<bool>(new TrustDialog(projectPath, extensionFolders));

    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        ShowAsync<bool>(new ConfirmDialog(title, message, confirmLabel));

    /// <inheritdoc/>
    public Task<SampleTargetChoice> ConfirmSampleTargetAsync(string sampleName, string targetFolder) =>
        ShowAsync<SampleTargetChoice>(new SampleTargetDialog(sampleName, targetFolder));

    /// <inheritdoc/>
    public Task<UnloadChoice> ConfirmUnsavedAsync(IReadOnlyList<UnsavedFile> files) =>
        ShowAsync<UnloadChoice>(new UnsavedChangesDialog(files));

    /// <inheritdoc/>
    public async Task<RecoveryAnswer> ConfirmRecoverAsync(IReadOnlyList<RecoveryFile> files)
    {
        var dialog = new RecoverDialog(files);
        await ShowAsync<RecoveryChoice>(dialog).ConfigureAwait(true);
        return dialog.Answer;
    }

    /// <inheritdoc/>
    public Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues) =>
        ShowAsync<object>(new IssuesDialog(title, issues));

    /// <inheritdoc/>
    public Task ShowKeyboardShortcutsAsync(KeyboardShortcutsViewModel sheet) =>
        ShowAsync<object>(new KeyboardShortcutsDialog(sheet));

    /// <inheritdoc/>
    public Task ShowCommandPaletteAsync(CommandPaletteViewModel palette) =>
        ShowAsync<object>(new CommandPaletteDialog(palette));

    /// <inheritdoc/>
    public Task ShowGoToAnythingAsync(GoToAnythingViewModel goTo) =>
        ShowAsync<object>(new GoToAnythingDialog(goTo));

    /// <inheritdoc/>
    public Task ShowAboutAsync(AboutViewModel about) =>
        ShowAsync<object>(new AboutDialog(about));

    /// <inheritdoc/>
    public Task<string?> ShowNewProjectAsync(NewProjectDialogViewModel dialog) =>
        ShowAsync<string>(new NewProjectDialog(dialog));

    /// <inheritdoc/>
    public Task ShowReferencesAsync(ReferenceListViewModel references) =>
        ShowAsync<object>(new ReferencesDialog { DataContext = references });
}

/// <summary>Result of a dialog shown without an owner.</summary>
public interface IDialogResult<out T>
{
    /// <summary>The dialog's result once it has closed, or <see langword="null"/> if cancelled.</summary>
    T? Result { get; }
}
