using System.Reactive.Concurrency;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Main;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;
using NetPrints.Workspace;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Composition root: creates the Avalonia service implementations, the main view model and the
/// main window (no DI container).
/// </summary>
public sealed class EditorComposition : IDisposable
{
    private readonly EditorServices services;

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    public EditorComposition(EditorHostServices host)
    {
        var windows = new WindowService();
        services = new EditorServices(host, windows,
            new EditorDialogs(() => windows.ActiveWindow),
            new StorageFilePickerService(() => windows.ActiveWindow),
            new ProcessLauncher());
    }

    /// <summary>The composed host services.</summary>
    public EditorContext Context => services.Context;

    /// <summary>The concrete window service (not just <see cref="IWindowService"/>, for callers that need <see cref="WindowService.ClassEditorWindows"/> or <see cref="WindowService.MainWindow"/>).</summary>
    public WindowService Windows => services.Windows;

    /// <summary>The main window's view model, created by <see cref="CreateMainWindow"/>, or <see langword="null"/> before it is called.</summary>
    public MainEditorViewModel? MainEditor => services.MainEditor;

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => services.InstallUnhandledExceptionHandler();

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateMainWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public Task StartAsync(IReadOnlyList<string> args) => services.StartAsync(args);

    /// <summary>Creates the main window and its view model.</summary>
    public MainWindow CreateMainWindow() => services.CreateMainWindow();

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes <see cref="MainEditor"/>, if created, and the
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

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    /// <param name="windows">The window service, already constructed by the caller so it can hand the same
    /// instance to <paramref name="dialogs"/>/<paramref name="filePicker"/>'s active-window accessor.</param>
    /// <param name="dialogs">Shows modal dialogs (errors, references): Avalonia's in production, a test double in the headless UI tests.</param>
    /// <param name="filePicker">Opens native file/save pickers: Avalonia's in production, a test double in the headless UI tests.</param>
    /// <param name="processes">Starts external processes: the real launcher in production, a test double in the headless UI tests.</param>
    public EditorServices(EditorHostServices host, WindowService windows, IEditorDialogs dialogs, IFilePickerService filePicker, IProcessLauncher processes)
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
            codeAnalysis);
    }

    /// <summary>The composed host services.</summary>
    public EditorContext Context { get; }

    /// <summary>The concrete window service.</summary>
    public WindowService Windows { get; }

    /// <summary>The main window's view model, created by <see cref="CreateMainWindow"/>, or <see langword="null"/> before it is called.</summary>
    public MainEditorViewModel? MainEditor { get; private set; }

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() =>
        new UnhandledExceptionHandler(Context.Dialogs, Context.Dispatcher, Context.LoggerFactory.CreateLogger<UnhandledExceptionHandler>());

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateMainWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public async Task StartAsync(IReadOnlyList<string> args)
    {
        MainEditorViewModel mainEditor = MainEditor ?? throw new InvalidOperationException($"{nameof(CreateMainWindow)} must be called first.");
        if (hostChannelError is not null)
        {
            await Context.Dialogs.ShowErrorAsync("Host channel unavailable", hostChannelError);
        }

        await mainEditor.ReportExtensionFailuresAsync();
        await mainEditor.OpenStartupProjectAsync(args);
    }

    /// <summary>Creates the main window and its view model.</summary>
    public MainWindow CreateMainWindow()
    {
        MainEditor?.Dispose();
        MainEditor = new MainEditorViewModel(Context);
        var window = new MainWindow { DataContext = MainEditor };
        Windows.MainWindow = window;
        return window;
    }

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes <see cref="MainEditor"/>, if created, and the
    /// code analysis host (editor-services.md §6: "disposed with the main window").
    /// </summary>
    public void Dispose()
    {
        MainEditor?.Dispose();
        persistenceBinding.Dispose();
        codeAnalysis.Dispose();
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
