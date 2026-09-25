using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The services a view model can use. Hosts (desktop, tests, future VS/browser hosts) provide
/// their own implementations; every member is required (no defaults).
/// </summary>
/// <param name="Scheduler">Scheduler for time-based work such as the search throttle (virtual time in tests).</param>
/// <param name="CodeRefreshScheduler">
/// Scheduler for the class editor's generated-code preview loop (PAR-34), separate from
/// <paramref name="Scheduler"/> so a host can silence the periodic real-time refresh (e.g. a
/// snapshot test capturing that preview) without also virtualizing the search throttle.
/// </param>
/// <param name="CreateMessenger">Creates a messenger for one editor scope (a class editor window).</param>
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
    Func<IMessenger> CreateMessenger);
