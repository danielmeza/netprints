using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;
using NetPrints.Serialization;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The services a view model can use. Hosts (desktop, tests, future VS/browser hosts) provide
/// their own implementations; every member is required (no defaults).
/// </summary>
/// <param name="FilePicker">Opens native file/save pickers.</param>
/// <param name="Dialogs">Shows modal dialogs (errors, references).</param>
/// <param name="Clipboard">Reads and writes the system clipboard.</param>
/// <param name="Dispatcher">Posts and invokes work on the UI thread.</param>
/// <param name="Reflection">Owns the reflection provider for the open project.</param>
/// <param name="Windows">Opens, activates and closes class editor windows.</param>
/// <param name="Processes">Starts external processes and reports their output.</param>
/// <param name="Scheduler">Scheduler for time-based work such as the search throttle (virtual time in tests).</param>
/// <param name="CreateMessenger">Creates a messenger for one editor scope (a class editor window).</param>
/// <param name="LoggerFactory">Creates the loggers editor-scope services log through (P1).</param>
/// <param name="Projects">Loads, edits, creates and builds the open project's <c>.csproj</c>
/// (project-system.md §4).</param>
/// <param name="Persistence">Loads, saves and adds class graphs of the open project
/// (document-format.md §2.8).</param>
/// <param name="Extensions">Holds the registry of loaded extensions.</param>
/// <param name="HostChannel">The channel to the application hosting the editor.</param>
/// <param name="Settings">Reads and writes the user's settings file.</param>
/// <param name="CodeAnalysis">Debounced live analysis of the open project's generated code (editor-services.md §2).</param>
/// <param name="RunState">Follows the last compile and launched program, for the automation agent's failure diagnostics.</param>
/// <param name="Backups">Where and how often the open project's unsaved files are backed up, or <see langword="null"/> for no backups (tests that do not exercise them).</param>
/// <param name="WindowStateService">Restores and saves the main window's bounds, or <see langword="null"/> for none (tests that do not exercise it).</param>
/// <param name="StateStore">The per-user state files the layout and the sessions are kept in, or <see langword="null"/> for none (tests that do not exercise them).</param>
/// <param name="Locations">The parent folder New project and the samples use, or <see langword="null"/> for the user's documents folder with the state store (tests that do not exercise it).</param>
/// <param name="Contributions">Places the editor contributions of the loaded extensions in the contribution groups, or <see langword="null"/> when the host does not show them (tests that do not exercise it).</param>
/// <param name="Recent">The recent projects list that opening or creating a project updates, or <see langword="null"/> for none (tests that do not exercise it).</param>
[Singleton<EditorContext>(Factory = "Create")]
public sealed record EditorContext(
    IFilePickerService FilePicker,
    IEditorDialogs Dialogs,
    IClipboardService Clipboard,
    IUiDispatcher Dispatcher,
    IReflectionHost Reflection,
    IWindowService Windows,
    IProcessLauncher Processes,
    IScheduler Scheduler,
    Func<IMessenger> CreateMessenger,
    ILoggerFactory LoggerFactory,
    IProjectSystem Projects,
    ProjectPersistence Persistence,
    IExtensionHost Extensions,
    IHostChannel HostChannel,
    ISettingsStore Settings,
    ICodeAnalysisHost CodeAnalysis,
    RunStateTracker RunState,
    BackupOptions? Backups = null,
    RecentProjects? Recent = null,
    WindowStateService? WindowStateService = null,
    IEditorStateStore? StateStore = null,
    ProjectLocations? Locations = null,
    ExtensionContributionRecorder? Contributions = null)
{
    /// <summary>Creates the facade from the container; the optional members are the ones the host registered.</summary>
    /// <param name="services">Resolves the optional members.</param>
    /// <param name="filePicker">Opens native file/save pickers.</param>
    /// <param name="dialogs">Shows modal dialogs.</param>
    /// <param name="clipboard">Reads and writes the system clipboard.</param>
    /// <param name="dispatcher">Posts and invokes work on the UI thread.</param>
    /// <param name="reflection">Owns the reflection provider.</param>
    /// <param name="windows">Opens, activates and closes windows.</param>
    /// <param name="processes">Starts external processes.</param>
    /// <param name="scheduler">Scheduler for time-based work.</param>
    /// <param name="createMessenger">Creates a messenger for one editor scope.</param>
    /// <param name="loggerFactory">Creates the loggers.</param>
    /// <param name="projects">The project system.</param>
    /// <param name="persistence">Loads, saves and adds class graphs.</param>
    /// <param name="extensions">The extension host.</param>
    /// <param name="hostChannel">The channel to the hosting application.</param>
    /// <param name="settings">The settings store.</param>
    /// <param name="codeAnalysis">Live analysis of the open project.</param>
    /// <param name="runState">Follows the last compile and program.</param>
    /// <returns>The facade.</returns>
    internal static EditorContext Create(
        IServiceProvider services,
        IFilePickerService filePicker,
        IEditorDialogs dialogs,
        IClipboardService clipboard,
        IUiDispatcher dispatcher,
        IReflectionHost reflection,
        IWindowService windows,
        IProcessLauncher processes,
        IScheduler scheduler,
        Func<IMessenger> createMessenger,
        ILoggerFactory loggerFactory,
        IProjectSystem projects,
        ProjectPersistence persistence,
        IExtensionHost extensions,
        IHostChannel hostChannel,
        ISettingsStore settings,
        ICodeAnalysisHost codeAnalysis,
        RunStateTracker runState) =>
        new(
            filePicker,
            dialogs,
            clipboard,
            dispatcher,
            reflection,
            windows,
            processes,
            scheduler,
            createMessenger,
            loggerFactory,
            projects,
            persistence,
            extensions,
            hostChannel,
            settings,
            codeAnalysis,
            runState,
            services.GetService<BackupOptions>(),
            services.GetService<RecentProjects>(),
            services.GetService<WindowStateService>(),
            services.GetService<IEditorStateStore>(),
            services.GetService<ProjectLocations>(),
            services.GetService<ExtensionContributionRecorder>());
}
