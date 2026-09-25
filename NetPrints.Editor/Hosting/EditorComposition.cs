using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Composition root: creates the Avalonia service implementations, the main view model and the
/// main window (no DI container).
/// </summary>
public sealed class EditorComposition
{
    /// <param name="customize">Optional hook to replace services (used by the headless UI tests).</param>
    public EditorComposition(Func<EditorContext, EditorContext>? customize = null)
    {
        var dispatcher = new AvaloniaUiDispatcher();
        Windows = new WindowService();
        var context = new EditorContext(
            new StorageFilePickerService(() => Windows.ActiveWindow),
            new EditorDialogs(() => Windows.ActiveWindow),
            new AvaloniaClipboardService(() => Windows.ActiveWindow),
            dispatcher,
            new ReflectionHost(dispatcher),
            Windows,
            new ProcessLauncher(),
            DefaultScheduler.Instance,
            DefaultScheduler.Instance,
            () => new WeakReferenceMessenger());
        Context = customize?.Invoke(context) ?? context;
    }

    public EditorContext Context { get; }

    public WindowService Windows { get; }

    public MainEditorVM? MainEditor { get; private set; }

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => new UnhandledExceptionHandler(Context.Dialogs, Context.Dispatcher);

    /// <summary>Creates the main window and its view model.</summary>
    public MainWindow CreateMainWindow()
    {
        MainEditor = new MainEditorVM(Context);
        var window = new MainWindow { DataContext = MainEditor };
        Windows.MainWindow = window;
        return window;
    }
}
