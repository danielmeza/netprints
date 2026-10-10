using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

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
    /// <param name="backups">Where the open project's unsaved files are backed up; <see langword="null"/> for no backups.</param>
    /// <param name="stateStore">Keeps the dock layout and the sessions; <see langword="null"/> for neither.</param>
    public TestComposition(EditorHostServices host, IEditorDialogs dialogs, IFilePickerService filePicker, IProcessLauncher processes, BackupOptions? backups = null, IEditorStateStore? stateStore = null) =>
        services = new EditorServices(host, new WindowService(), dialogs, filePicker, processes, backups, stateStore: stateStore);

    /// <summary>The composed host services, with the given test doubles standing in for the Avalonia dialogs, file picker and process launcher.</summary>
    public EditorContext Context => services.Context;

    /// <summary>The concrete window service.</summary>
    public WindowService Windows => services.Windows;

    /// <summary>The project flows of the shell, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IProjectActions? ProjectActions => services.ProjectActions;

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => services.InstallUnhandledExceptionHandler();

    /// <summary>
    /// Reports what went wrong before the window existed, then opens the project named on the
    /// command line, if any (mirrors <see cref="EditorComposition.StartAsync"/>). Call after
    /// <see cref="CreateShellWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public Task StartAsync(IReadOnlyList<string> args) => services.StartAsync(args);

    /// <summary>Creates the shell window, as <see cref="EditorComposition.CreateShellWindow"/> does.</summary>
    public ShellWindow CreateShellWindow() => services.CreateShellWindow();

    /// <summary>The frozen registry the shell was generated from, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IContributionRegistry? Registry => services.Registry;

    /// <summary>The composed shell state, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public ShellViewModel? Shell => services.Shell;

    /// <summary>The shell API over the docking layout, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IShell? ShellApi => services.ShellApi;

    /// <summary>The invoker of the registered commands, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public CommandInvoker? Commands => services.Commands;

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry, disposes
    /// the shell, if created, and the code analysis host.
    /// </summary>
    public void Dispose() => services.Dispose();
}
