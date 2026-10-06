using System.Reactive.Concurrency;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;
using NetPrints.Workspace;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Composition root: creates the Avalonia service implementations and the shell window (no DI container).
/// </summary>
public sealed class EditorComposition : IDisposable
{
    private readonly EditorServices services;

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    public EditorComposition(EditorHostServices host)
    {
        var windows = new WindowService();
        EditorDataPaths paths = EditorDataPaths.Resolve();
        var fileSystem = new RealEditorFileSystem();
        var stateStore = new JsonEditorStateStore(paths, fileSystem, host.LoggerFactory.CreateLogger<JsonEditorStateStore>());
        services = new EditorServices(host, windows,
            new EditorDialogs(() => windows.ActiveWindow),
            new StorageFilePickerService(() => windows.ActiveWindow),
            new ProcessLauncher(),
            new BackupOptions(paths, fileSystem, TimeProvider.System, BackupService.ResolveDelay(Environment.GetEnvironmentVariable)),
            new RecentProjects(stateStore, fileSystem, TimeProvider.System),
            new WindowStateService(stateStore),
            stateStore);
    }

    /// <summary>The composed host services.</summary>
    public EditorContext Context => services.Context;

    /// <summary>The concrete window service (not just <see cref="IWindowService"/>, for callers that need <see cref="WindowService.MainWindow"/>).</summary>
    public WindowService Windows => services.Windows;

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => services.InstallUnhandledExceptionHandler();

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateShellWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public Task StartAsync(IReadOnlyList<string> args) => services.StartAsync(args);

    /// <summary>The composed shell state, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public ShellViewModel? Shell => services.Shell;

    /// <summary>The shell API over the docking layout, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IShell? ShellApi => services.ShellApi;

    /// <summary>The invoker of the registered commands, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public CommandInvoker? Commands => services.Commands;

    /// <summary>Creates the shell window over a project service (no legacy window opens).</summary>
    public ShellWindow CreateShellWindow() => services.CreateShellWindow();

    /// <summary>
    /// Asks whether the application may exit, as closing the window does: waits for a build, asks to stop a running
    /// program and asks about unsaved files. Passes when the shell does not exist yet.
    /// </summary>
    /// <param name="cancellationToken">Cancels the prompts.</param>
    /// <returns><see langword="true"/> to go on, <see langword="false"/> to keep the application running.</returns>
    public Task<bool> ConfirmExitAsync(CancellationToken cancellationToken) => services.ConfirmExitAsync(cancellationToken);

    /// <summary>Writes the backups of the open project that are still waiting.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public Task FlushBackupsAsync() => services.FlushBackupsAsync();

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes the shell, if created, and the
    /// code analysis host (editor-services.md §6: "disposed with the main window").
    /// </summary>
    public void Dispose() => services.Dispose();
}

/// <summary>
/// Builds the Avalonia-agnostic half of the composition root (project system, persistence, reflection,
/// live analysis, the main view model and window) from the dialogs, file picker and process launcher the
/// caller supplies: <see cref="EditorComposition"/> passes the Avalonia implementations, and the headless
/// UI tests' own composition passes test doubles instead. Passing them in through the constructor is
/// ordinary dependency injection, not a customization hook (ED-T14): both callers exercise this exact
/// same startup order, so a change to it (R2-06) is caught by the headless UI tests too, not only by the
/// slow E2E suite.
/// </summary>
internal sealed class EditorServices : IDisposable
{
    /// <summary>
    /// <c>NetPrints.Sdk</c> version substituted into a new project's template (project-system.md §4):
    /// the editor's own <see cref="AssemblyInformationalVersionAttribute"/>, which MinVer stamps at
    /// build time, with the <c>+&lt;sha&gt;</c> build-metadata suffix stripped (release-and-docs.md,
    /// "Editor version").
    /// </summary>
    private static readonly string NetPrintsSdkVersion = EditorSdkVersion.Resolve(typeof(EditorServices).Assembly);

    private readonly string? hostChannelError;
    private readonly PersistenceBinding persistenceBinding;
    private readonly ICodeAnalysisHost codeAnalysis;
    private readonly RunStateTracker runState;
    private ShellHost? shellHost;

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    /// <param name="windows">The window service, already constructed by the caller so it can hand the same
    /// instance to <paramref name="dialogs"/>/<paramref name="filePicker"/>'s active-window accessor.</param>
    /// <param name="dialogs">Shows modal dialogs (errors, references): Avalonia's in production, a test double in the headless UI tests.</param>
    /// <param name="filePicker">Opens native file/save pickers: Avalonia's in production, a test double in the headless UI tests.</param>
    /// <param name="processes">Starts external processes: the real launcher in production, a test double in the headless UI tests.</param>
    /// <param name="backups">Where the open project's unsaved files are backed up; <see langword="null"/> (the headless tests) for no backups and no clean-up of the user's data folder.</param>
    /// <param name="recent">The recent projects list; <see langword="null"/> (the headless tests) to keep none.</param>
    /// <param name="windowState">Restores and saves the main window's bounds; <see langword="null"/> (the headless tests) to leave the window alone.</param>
    /// <param name="stateStore">Keeps the dock layout and each project's session; <see langword="null"/> (the headless tests) to save and restore neither.</param>
    public EditorServices(EditorHostServices host, WindowService windows, IEditorDialogs dialogs, IFilePickerService filePicker, IProcessLauncher processes, BackupOptions? backups = null, RecentProjects? recent = null, WindowStateService? windowState = null, IEditorStateStore? stateStore = null)
    {
        hostChannelError = host.HostChannelError;
        var dispatcher = new AvaloniaUiDispatcher();
        Windows = windows;

        IProjectSystem projects = host.MsBuildAvailable
            ? new MsBuildProjectSystem(new ProjectSystemOptions(new ExtensionProjectProperties(host.Extensions), NetPrintsSdkVersion), new ProcessRunner(),
                host.LoggerFactory.CreateLogger<MsBuildProjectSystem>())
            : new NoSdkProjectSystem();

        (DocumentFormatRegistry formats, IDocumentMapper mapper) = PersistenceBinding.CreateSerializers(host.Extensions.Current, host.LoggerFactory);
        var persistence = new ProjectPersistence(projects, formats, mapper,
            (directory, watch) => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, host.LoggerFactory.CreateLogger<FileSystemDocumentStore>(), watch),
            host.LoggerFactory.CreateLogger<ProjectPersistence>());
        persistenceBinding = PersistenceBinding.Bind(persistence, host.Extensions, host.LoggerFactory);

        var reflection = new ReflectionHost(dispatcher, host.Extensions, host.LoggerFactory.CreateLogger<ReflectionHost>());
        codeAnalysis = new CodeAnalysisHost(reflection, host.Extensions, DefaultScheduler.Instance, dispatcher, host.LoggerFactory.CreateLogger<CodeAnalysisHost>());

        runState = new RunStateTracker(processes);

        Context = new EditorContext(
            filePicker,
            dialogs,
            new AvaloniaClipboardService(() => Windows.ActiveWindow),
            dispatcher,
            reflection,
            Windows,
            processes,
            DefaultScheduler.Instance,
            () => new WeakReferenceMessenger(),
            host.LoggerFactory,
            projects,
            persistence,
            host.Extensions,
            host.HostChannel,
            host.Settings,
            codeAnalysis,
            runState,
            backups,
            recent,
            windowState,
            stateStore);
    }

    /// <summary>The composed host services.</summary>
    public EditorContext Context { get; }

    /// <summary>The concrete window service.</summary>
    public WindowService Windows { get; }

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() =>
        new UnhandledExceptionHandler(Context.Dialogs, Context.Dispatcher, Context.LoggerFactory.CreateLogger<UnhandledExceptionHandler>());

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateShellWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public async Task StartAsync(IReadOnlyList<string> args)
    {
        ProjectLoader loader = shellHost?.Actions.Loader ?? throw new InvalidOperationException($"{nameof(CreateShellWindow)} must be called first.");
        if (hostChannelError is not null)
        {
            await Context.Dialogs.ShowErrorAsync("Host channel unavailable", hostChannelError);
        }

        await loader.ReportExtensionFailuresAsync();
        await CleanUpBackupsAsync();
        await loader.OpenStartupProjectAsync(args);
    }

    // Off the UI thread: a folder on an unreachable share can take long to answer. Runs before a project opens, so nothing writes beside it.
    private Task CleanUpBackupsAsync() =>
        Context.Backups is { } options
            ? Task.Run(() => BackupService.CleanUp(options.Paths, options.FileSystem, options.Time, Context.LoggerFactory.CreateLogger<BackupService>()))
            : Task.CompletedTask;

    /// <summary>The frozen registry the shell was generated from, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IContributionRegistry? Registry => shellHost?.Registry;

    /// <summary>The composed shell state, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public ShellViewModel? Shell => shellHost?.Shell;

    /// <summary>The shell API over the docking layout, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IShell? ShellApi => shellHost?.Adapter;

    /// <summary>The invoker of the registered commands, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public CommandInvoker? Commands => shellHost?.Invoker;

    /// <summary>The project flows of the shell, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    internal IProjectActions? ProjectActions => shellHost?.Actions;

    /// <summary>Asks whether the application may exit; passes when the shell does not exist yet.</summary>
    /// <param name="cancellationToken">Cancels the prompts.</param>
    /// <returns><see langword="true"/> to go on.</returns>
    public Task<bool> ConfirmExitAsync(CancellationToken cancellationToken) =>
        shellHost?.Actions.ConfirmExitAsync(cancellationToken) ?? Task.FromResult(true);

    /// <summary>Writes the backups of the open project that are still waiting; the exit cleanup awaits it before disposing the composition.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public Task FlushBackupsAsync() => shellHost?.Actions.Loader.FlushBackupsAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Creates the shell window: the registry, <see cref="ShellViewModel"/>, the docking adapter and the project flows
    /// that open, create and close projects.
    /// </summary>
    public ShellWindow CreateShellWindow()
    {
        shellHost?.Dispose();
        shellHost = ShellHost.Create(Context);
        Windows.MainWindow = shellHost.Window;
        return shellHost.Window;
    }

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes the shell, if created, and the
    /// code analysis host (editor-services.md §6: "disposed with the main window").
    /// </summary>
    public void Dispose()
    {
        shellHost?.Dispose();
        persistenceBinding.Dispose();
        codeAnalysis.Dispose();
        runState.Dispose();
    }
}

/// <summary>
/// Derives the editor's own version for <see cref="ProjectSystemOptions.NetPrintsSdkVersion"/>
/// (release-and-docs.md, "Editor version").
/// </summary>
internal static class EditorSdkVersion
{
    /// <summary>Used when the assembly carries no informational version, e.g. run without MinVer having stamped one.</summary>
    internal const string Fallback = "0.1.0-dev";

    /// <summary>The editor assembly's informational version, stripped of its <c>+&lt;sha&gt;</c> suffix, or <see cref="Fallback"/>.</summary>
    internal static string Resolve(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational is null ? Fallback : StripBuildMetadata(informational);
    }

    /// <summary>Strips MinVer's <c>+&lt;sha&gt;</c> build-metadata suffix, if present.</summary>
    internal static string StripBuildMetadata(string version) =>
        version.Contains('+', StringComparison.Ordinal) ? version[..version.IndexOf('+', StringComparison.Ordinal)] : version;
}
