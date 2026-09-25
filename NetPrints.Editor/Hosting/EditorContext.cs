using CommunityToolkit.Mvvm.Messaging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The services a view model can use. Hosts (desktop, tests, future VS/browser hosts) provide
/// their own implementations.
/// </summary>
public sealed record EditorContext(
    IFilePickerService FilePicker,
    IEditorDialogs Dialogs,
    IClipboardService Clipboard,
    IUiDispatcher Dispatcher,
    IReflectionHost Reflection,
    IWindowService Windows,
    IProcessLauncher Processes)
{
    /// <summary>Creates a messenger for one editor scope (a class editor window).</summary>
    public Func<IMessenger> CreateMessenger { get; init; } = () => new WeakReferenceMessenger();
}
