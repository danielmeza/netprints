using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Modal Avalonia dialogs owned by the active window.</summary>
public sealed class EditorDialogs(Func<Window?> owner) : IEditorDialogs
{
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

    public Task ShowErrorAsync(string title, string message) =>
        ShowAsync<object>(new ErrorDialog(title, message));

    public Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial) =>
        ShowAsync<TypeSpecifier>(new SelectTypeDialog(types, initial));

    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods) =>
        ShowAsync<MethodSpecifier>(new SelectMethodDialog(methods));

    public async Task ShowReferencesAsync(ReferenceListVM references)
    {
        try
        {
            await ShowAsync<object>(new ReferencesDialog { DataContext = references });
        }
        finally
        {
            references.Dispose();
        }
    }
}

/// <summary>Result of a dialog shown without an owner.</summary>
public interface IDialogResult<out T>
{
    T? Result { get; }
}
