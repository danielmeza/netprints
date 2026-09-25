using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Testing.Ui.Driving;

/// <summary>What a driver can do beyond the common input and queries.</summary>
[Flags]
public enum UiCapabilities
{
    None = 0,

    /// <summary>Platform (GTK) file and folder pickers.</summary>
    NativeDialogs = 1,

    /// <summary>A real window manager: minimize, restore and activate through the WM.</summary>
    WindowManager = 2,

    /// <summary>The real pointer cursor shape can be read (XFixes).</summary>
    RealCursor = 4,

    /// <summary>Operating-system drag and drop between controls.</summary>
    OsDragDrop = 8,

    /// <summary>The compiled program is started as a real process and its output can be read.</summary>
    ProcessOutput = 16,
}

public enum UiButton
{
    Left,
    Middle,
    Right,
    Back,
}

/// <summary>A point in a window, in the driver's coordinate space (see <see cref="IUiDriver.At"/>).</summary>
public readonly record struct UiTarget(string Window, double X, double Y)
{
    public UiTarget Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}

/// <summary>
/// Drives the editor's UI. Only page objects call it; tests and Screenplay tasks go through page
/// objects. Every operation takes a cancellation token (the test's token).
/// </summary>
public interface IUiDriver : IAsyncDisposable
{
    string Name { get; }

    UiCapabilities Capabilities { get; }

    /// <summary>Finds the elements that match a query (a snapshot; poll with <see cref="UiWait"/>).</summary>
    Task<IReadOnlyList<AutomationElement>> FindAllAsync(AutomationQuery query, CancellationToken cancellationToken);

    /// <summary>A point inside an element, as a fraction of its size (0.5, 0.5 = center).</summary>
    UiTarget At(AutomationElement element, double fractionX = 0.5, double fractionY = 0.5);

    /// <summary>Lets the UI process pending work (layout, bindings, rendering).</summary>
    Task SettleAsync(CancellationToken cancellationToken);

    Task MoveAsync(UiTarget target, CancellationToken cancellationToken);

    Task ClickAsync(UiTarget target, UiButton button, int clickCount, CancellationToken cancellationToken);

    /// <summary>Presses at <paramref name="from"/>, moves in steps to <paramref name="to"/>, releases.</summary>
    Task DragAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken);

    /// <summary>Presses and moves, without releasing (to observe state during a drag).</summary>
    Task PressAndMoveAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken);

    /// <summary>Releases a button pressed by <see cref="PressAndMoveAsync"/>.</summary>
    Task ReleaseAsync(UiTarget at, UiButton button, CancellationToken cancellationToken);

    /// <summary>Scrolls the wheel; positive is up (zoom in on the canvas).</summary>
    Task WheelAsync(UiTarget target, double delta, CancellationToken cancellationToken);

    Task TypeAsync(string text, CancellationToken cancellationToken);

    /// <summary>Presses a key chord such as "Ctrl+Z", "Delete", "Enter" or "Escape".</summary>
    Task PressAsync(string chord, CancellationToken cancellationToken);

    /// <summary>A PNG screenshot of a window.</summary>
    Task<UiImage> ScreenshotAsync(string window, CancellationToken cancellationToken);

    /// <summary>The name of the pointer cursor (<see cref="UiCapabilities.RealCursor"/>).</summary>
    Task<string?> CursorNameAsync(CancellationToken cancellationToken);

    /// <summary>Minimizes a window through the window manager (<see cref="UiCapabilities.WindowManager"/>).</summary>
    Task MinimizeAsync(string window, CancellationToken cancellationToken);

    /// <summary>Whether the window manager shows the window as minimized (<see cref="UiCapabilities.WindowManager"/>).</summary>
    Task<bool> IsMinimizedAsync(string window, CancellationToken cancellationToken);

    /// <summary>Output written by programs the editor ran (<see cref="UiCapabilities.ProcessOutput"/>).</summary>
    Task<string> ProgramOutputAsync(CancellationToken cancellationToken);

    /// <summary>Diagnostics: a dump of the UI elements with automation ids.</summary>
    Task<string> DumpAsync(CancellationToken cancellationToken);
}
