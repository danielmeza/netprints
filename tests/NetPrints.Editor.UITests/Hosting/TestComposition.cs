using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Main;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// Builds the same <see cref="EditorContext"/> as <see cref="EditorComposition"/> from
/// <see cref="EditorHostServices"/>, but with the given dialogs, file picker and process launcher
/// standing in for the production Avalonia implementations. Production code takes no test-only hook
/// (ED-T14), so the headless UI tests own this composition instead of customizing
/// <see cref="EditorComposition"/>.
/// </summary>
public sealed class TestComposition : IDisposable
{
    private readonly string? hostChannelError;
    private readonly PersistenceBinding persistenceBinding;
    private readonly ICodeAnalysisHost codeAnalysis;

    /// <param name="host">Process-wide services created once by the host.</param>
    /// <param name="dialogs">Stands in for the production modal dialogs.</param>
    /// <param name="filePicker">Stands in for the production native file/save pickers.</param>
    /// <param name="processes">Stands in for the production process launcher.</param>
    public TestComposition(EditorHostServices host, IEditorDialogs dialogs, IFilePickerService filePicker, IProcessLauncher processes)
    {
        hostChannelError = host.HostChannelError;
        var dispatcher = new AvaloniaUiDispatcher();
        Windows = new WindowService();

        IProjectSystem projects = host.MsBuildAvailable
            ? new MsBuildProjectSystem(new ProjectSystemOptions(new ExtensionProjectProperties(host.Extensions), EditorComposition.NetPrintsSdkVersion), new ProcessRunner(),
                host.LoggerFactory.CreateLogger<MsBuildProjectSystem>())
            : new NoSdkProjectSystem();

        (DocumentFormatRegistry formats, IDocumentMapper mapper) = PersistenceBinding.CreateSerializers(host.Extensions.Current, host.LoggerFactory);
        var persistence = new ProjectPersistence(projects, formats, mapper,
            directory => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, host.LoggerFactory.CreateLogger<FileSystemDocumentStore>()),
            host.LoggerFactory.CreateLogger<ProjectPersistence>());
        persistenceBinding = PersistenceBinding.Bind(persistence, host.Extensions, host.LoggerFactory);

        var reflection = new ReflectionHost(dispatcher, host.Extensions, host.LoggerFactory.CreateLogger<ReflectionHost>());
        codeAnalysis = new CodeAnalysisHost(reflection, host.Extensions, DefaultScheduler.Instance, dispatcher, host.LoggerFactory.CreateLogger<CodeAnalysisHost>());

        Context = new EditorContext(filePicker, dialogs, new AvaloniaClipboardService(() => Windows.ActiveWindow), dispatcher, reflection, Windows,
            processes, DefaultScheduler.Instance, () => new WeakReferenceMessenger(), host.LoggerFactory, projects, persistence, host.Extensions,
            host.HostChannel, host.Settings, codeAnalysis);
    }

    /// <summary>The composed host services, with the given test doubles standing in for the Avalonia dialogs, file picker and process launcher.</summary>
    public EditorContext Context { get; }

    /// <summary>The concrete window service.</summary>
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
    /// Reports what went wrong before the window existed, then opens the project named on the
    /// command line, if any (mirrors <see cref="EditorComposition.StartAsync"/>). Call after
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
    /// Stops rebinding persistence to the extension host's registry, disposes
    /// <see cref="MainEditor"/>, if created, and the code analysis host.
    /// </summary>
    public void Dispose()
    {
        MainEditor?.Dispose();
        persistenceBinding.Dispose();
        codeAnalysis.Dispose();
    }
}
