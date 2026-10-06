using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
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
/// <param name="Recent">The recent projects list that opening or creating a project updates, or <see langword="null"/> for none (tests that do not exercise it).</param>
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
    WindowStateService? WindowStateService = null);
