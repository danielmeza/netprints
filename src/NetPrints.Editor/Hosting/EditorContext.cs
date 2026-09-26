using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

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
/// <param name="CodeRefreshScheduler">
/// Scheduler for the class editor's generated-code preview loop (PAR-34), separate from
/// <paramref name="Scheduler"/> so a host can silence the periodic real-time refresh (e.g. a
/// snapshot test capturing that preview) without also virtualizing the search throttle.
/// </param>
/// <param name="CreateMessenger">Creates a messenger for one editor scope (a class editor window).</param>
/// <param name="LoggerFactory">Creates the loggers editor-scope services log through (P1).</param>
public sealed record EditorContext(
    IFilePickerService FilePicker,
    IEditorDialogs Dialogs,
    IClipboardService Clipboard,
    IUiDispatcher Dispatcher,
    IReflectionHost Reflection,
    IWindowService Windows,
    IProcessLauncher Processes,
    IScheduler Scheduler,
    IScheduler CodeRefreshScheduler,
    Func<IMessenger> CreateMessenger,
    ILoggerFactory LoggerFactory);
