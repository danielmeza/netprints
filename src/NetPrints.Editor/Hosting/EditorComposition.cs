using System.Reactive.Concurrency;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
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
    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    /// <param name="customize">Optional hook to replace services (used by the headless UI tests).</param>
    public EditorComposition(EditorHostServices host, Func<EditorContext, EditorContext>? customize = null)
    {
        hostChannelError = host.HostChannelError;
        var dispatcher = new AvaloniaUiDispatcher();
        Windows = new WindowService();

        IProjectSystem projects = host.MsBuildAvailable
            ? new MsBuildProjectSystem(new ProjectSystemOptions(new ExtensionProjectProperties(host.Extensions), NetPrintsSdkVersion), new ProcessRunner(),
                host.LoggerFactory.CreateLogger<MsBuildProjectSystem>())
            : new NoSdkProjectSystem();

        (DocumentFormatRegistry formats, IDocumentMapper mapper) = PersistenceBinding.CreateSerializers(host.Extensions.Current);
        var persistence = new ProjectPersistence(projects, formats, mapper,
            directory => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, host.LoggerFactory.CreateLogger<FileSystemDocumentStore>()),
            host.LoggerFactory.CreateLogger<ProjectPersistence>());
        persistenceBinding = PersistenceBinding.Bind(persistence, host.Extensions);

        var context = new EditorContext(
            new StorageFilePickerService(() => Windows.ActiveWindow),
            new EditorDialogs(() => Windows.ActiveWindow),
            new AvaloniaClipboardService(() => Windows.ActiveWindow),
            dispatcher,
            new ReflectionHost(dispatcher, host.Extensions, host.LoggerFactory.CreateLogger<ReflectionHost>()),
            Windows,
            new ProcessLauncher(),
            DefaultScheduler.Instance,
            DefaultScheduler.Instance,
            () => new WeakReferenceMessenger(),
            host.LoggerFactory,
            projects,
            persistence,
            host.Extensions,
            host.HostChannel,
            host.Settings);
        Context = customize?.Invoke(context) ?? context;
    }

    private readonly string? hostChannelError;
    private readonly PersistenceBinding persistenceBinding;

    /// <summary>
    /// Placeholder <c>NetPrints.Sdk</c> version substituted into a new project's template
    /// (project-system.md §4): MinVer is not wired up until sub-phase L (T109).
    /// </summary>
    private const string NetPrintsSdkVersion = "1.0.0-dev";

    /// <summary>The composed host services, possibly customized by the constructor's <c>customize</c> hook.</summary>
    public EditorContext Context { get; }

    /// <summary>The concrete window service (not just <see cref="IWindowService"/>, for callers that need <see cref="WindowService.ClassEditorWindows"/> or <see cref="WindowService.MainWindow"/>).</summary>
    public WindowService Windows { get; }

    /// <summary>The main window's view model, created by <see cref="CreateMainWindow"/>, or <see langword="null"/> before it is called.</summary>
    public MainEditorVM? MainEditor { get; private set; }

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
        MainEditorVM mainEditor = MainEditor ?? throw new InvalidOperationException($"{nameof(CreateMainWindow)} must be called first.");
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
        MainEditor = new MainEditorVM(Context);
        var window = new MainWindow { DataContext = MainEditor };
        Windows.MainWindow = window;
        return window;
    }

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>) and disposes <see cref="MainEditor"/>, if created.
    /// </summary>
    public void Dispose()
    {
        MainEditor?.Dispose();
        persistenceBinding.Dispose();
    }
}
