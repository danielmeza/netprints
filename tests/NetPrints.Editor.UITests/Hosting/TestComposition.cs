using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// Builds the same <see cref="EditorContext"/> as <see cref="EditorComposition"/>, through their shared
/// <see cref="EditorServices"/> (R2-06), but with the given dialogs, file picker and process launcher
/// standing in for the production Avalonia implementations. Production code takes no test-only hook
/// (ED-T14): the headless UI tests pass their own doubles into the same composition instead of
/// customizing <see cref="EditorComposition"/>.
/// </summary>
public sealed class TestComposition : IDisposable
{
    private readonly EditorServices services;

    /// <param name="host">Process-wide services created once by the host.</param>
    /// <param name="dialogs">Stands in for the production modal dialogs.</param>
    /// <param name="filePicker">Stands in for the production native file/save pickers.</param>
    /// <param name="processes">Stands in for the production process launcher.</param>
    public TestComposition(EditorHostServices host, IEditorDialogs dialogs, IFilePickerService filePicker, IProcessLauncher processes) =>
        services = new EditorServices(host, new WindowService(), dialogs, filePicker, processes);

    /// <summary>The composed host services, with the given test doubles standing in for the Avalonia dialogs, file picker and process launcher.</summary>
    public EditorContext Context => services.Context;

    /// <summary>The concrete window service.</summary>
    public WindowService Windows => services.Windows;

    /// <summary>The main window's view model, created by <see cref="CreateMainWindow"/>, or <see langword="null"/> before it is called.</summary>
    public MainEditorVM? MainEditor => services.MainEditor;

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => services.InstallUnhandledExceptionHandler();

    /// <summary>
    /// Reports what went wrong before the window existed, then opens the project named on the
    /// command line, if any (mirrors <see cref="EditorComposition.StartAsync"/>). Call after
    /// <see cref="CreateMainWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public Task StartAsync(IReadOnlyList<string> args) => services.StartAsync(args);

    /// <summary>Creates the main window and its view model.</summary>
    public MainWindow CreateMainWindow() => services.CreateMainWindow();

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry, disposes
    /// <see cref="MainEditor"/>, if created, and the code analysis host.
    /// </summary>
    public void Dispose() => services.Dispose();
}
