using System.Collections.Concurrent;
using System.Globalization;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Desktop.E2ETests.Driving;

/// <summary>
/// <see cref="IUiDriver"/> for the real editor on X11: elements and their screen bounds come
/// from the in-app automation agent; input is real (xdotool, through the X server and the
/// window manager); screenshots are taken with ImageMagick's import; window state is read with
/// xprop; the cursor with XFixes.
/// </summary>
public sealed class X11Driver(XServer server, EditorProcess editor, Tool tool) : IUiDriver
{
    private const int DragSteps = 12;
    private readonly ConcurrentDictionary<string, string> x11Windows = new();

    public string Name => "X11";

    public UiCapabilities Capabilities =>
        UiCapabilities.NativeDialogs | UiCapabilities.WindowManager | UiCapabilities.RealCursor | UiCapabilities.OsDragDrop | UiCapabilities.ProcessOutput;

    public Tool Tool => tool;

    public async Task<IReadOnlyList<AutomationElement>> FindAllAsync(AutomationQuery query, CancellationToken cancellationToken)
    {
        var elements = await editor.Client.FindAsync(query, cancellationToken);
        foreach (var element in elements)
        {
            if (element[AutomationPropertyNames.X11Window] is { Length: > 0 } id)
            {
                x11Windows[element.Window] = id;
            }
        }

        return elements;
    }

    public UiTarget At(AutomationElement element, double fractionX = 0.5, double fractionY = 0.5) =>
        new(element.Window, element.ScreenBounds.X + element.ScreenBounds.Width * fractionX, element.ScreenBounds.Y + element.ScreenBounds.Height * fractionY);

    public Task SettleAsync(CancellationToken cancellationToken) => editor.Client.SettleAsync(cancellationToken);

    private static string I(double value) => ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    private static string ButtonOf(UiButton button) => button switch
    {
        UiButton.Left => "1",
        UiButton.Middle => "2",
        UiButton.Right => "3",
        UiButton.Back => "8",
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private Task JumpToAsync(double x, double y, CancellationToken cancellationToken) =>
        tool.XdotoolAsync(cancellationToken, "mousemove", "--sync", I(x), I(y));

    /// <summary>
    /// Moves the pointer the way a hand does, through intermediate points, so the editor sees it
    /// leave controls (tooltips close, hover states end) instead of jumping onto whatever window
    /// is under the target, such as a pin's tooltip.
    /// </summary>
    private async Task MoveToAsync(double x, double y, CancellationToken cancellationToken)
    {
        string location = await tool.XdotoolAsync(cancellationToken, "getmouselocation", "--shell");
        var values = location.Split('\n').Select(l => l.Split('=')).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        double fromX = double.Parse(values["X"], CultureInfo.InvariantCulture);
        double fromY = double.Parse(values["Y"], CultureInfo.InvariantCulture);
        const int steps = 6;
        for (int i = 1; i < steps; i++)
        {
            await JumpToAsync(fromX + (x - fromX) * i / steps, fromY + (y - fromY) * i / steps, cancellationToken);
        }

        await JumpToAsync(x, y, cancellationToken);
        await SettleAsync(cancellationToken);
    }

    public async Task MoveAsync(UiTarget target, CancellationToken cancellationToken)
    {
        await MoveToAsync(target.X, target.Y, cancellationToken);
        await SettleAsync(cancellationToken);
    }

    public async Task ClickAsync(UiTarget target, UiButton button, int clickCount, CancellationToken cancellationToken)
    {
        await MoveToAsync(target.X, target.Y, cancellationToken);
        await tool.XdotoolAsync(cancellationToken, "click", "--repeat", clickCount.ToString(CultureInfo.InvariantCulture), "--delay", "60", ButtonOf(button));
        await SettleAsync(cancellationToken);
    }

    public async Task DragAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken)
    {
        await PressAndMoveAsync(from, to, button, cancellationToken);
        await ReleaseAsync(to, button, cancellationToken);
    }

    public async Task PressAndMoveAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken)
    {
        await MoveToAsync(from.X, from.Y, cancellationToken);
        await tool.XdotoolAsync(cancellationToken, "mousedown", ButtonOf(button));
        await SettleAsync(cancellationToken);
        for (int i = 1; i <= DragSteps; i++)
        {
            await JumpToAsync(from.X + (to.X - from.X) * i / DragSteps, from.Y + (to.Y - from.Y) * i / DragSteps, cancellationToken);
            await SettleAsync(cancellationToken);
        }
    }

    public async Task ReleaseAsync(UiTarget at, UiButton button, CancellationToken cancellationToken)
    {
        await JumpToAsync(at.X, at.Y, cancellationToken);
        await tool.XdotoolAsync(cancellationToken, "mouseup", ButtonOf(button));
        await SettleAsync(cancellationToken);
    }

    public async Task WheelAsync(UiTarget target, double delta, CancellationToken cancellationToken)
    {
        await MoveToAsync(target.X, target.Y, cancellationToken);
        int clicks = Math.Max(1, (int)Math.Round(Math.Abs(delta)));
        await tool.XdotoolAsync(cancellationToken, "click", "--repeat", clicks.ToString(CultureInfo.InvariantCulture), delta > 0 ? "4" : "5");
        await SettleAsync(cancellationToken);
    }

    public async Task TypeAsync(string text, CancellationToken cancellationToken)
    {
        await tool.XdotoolAsync(cancellationToken, "type", "--clearmodifiers", "--delay", "15", "--", text);
        await SettleAsync(cancellationToken);
    }

    /// <summary>Maps "Ctrl+Z", "Delete", "Enter", "Escape" … to xdotool key names.</summary>
    public static string KeyOf(string chord) => string.Join('+', chord.Split('+').Select(part => part switch
    {
        "Ctrl" => "ctrl",
        "Shift" => "shift",
        "Alt" => "alt",
        "Enter" => "Return",
        "Esc" => "Escape",
        var key when key.Length == 1 => key.ToLowerInvariant(),
        var key => key,
    }));

    public async Task PressAsync(string chord, CancellationToken cancellationToken)
    {
        await tool.XdotoolAsync(cancellationToken, "key", "--clearmodifiers", KeyOf(chord));
        await SettleAsync(cancellationToken);
    }

    private string X11WindowOf(string window) =>
        x11Windows.TryGetValue(window, out var id) ? id : throw new InvalidOperationException($"No X11 window known for {window}; find an element in it first.");

    public async Task<UiImage> ScreenshotAsync(string window, CancellationToken cancellationToken)
    {
        await SettleAsync(cancellationToken);
        return new UiImage(await tool.RunBytesAsync("import", ["-window", X11WindowOf(window), "png:-"], cancellationToken));
    }

    /// <summary>A screenshot of the whole screen (all windows, including platform dialogs).</summary>
    public async Task<UiImage> ScreenAsync(CancellationToken cancellationToken) =>
        new(await tool.RunBytesAsync("import", ["-window", "root", "png:-"], cancellationToken));

    public Task<string?> CursorNameAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(XFixes.CursorName(server.DisplayName));

    /// <summary>The top-level (window manager) window of a client window.</summary>
    public async Task MinimizeAsync(string window, CancellationToken cancellationToken)
    {
        await tool.XdotoolAsync(cancellationToken, "windowminimize", "--sync", X11WindowOf(window));
        await SettleAsync(cancellationToken);
    }

    public async Task<bool> IsMinimizedAsync(string window, CancellationToken cancellationToken)
    {
        var (_, state) = await tool.TryRunAsync("xprop", cancellationToken, "-id", X11WindowOf(window), "WM_STATE", "_NET_WM_STATE");
        return state.Contains("Iconic", StringComparison.Ordinal) || state.Contains("_NET_WM_STATE_HIDDEN", StringComparison.Ordinal);
    }

    public Task<string> ProgramOutputAsync(CancellationToken cancellationToken) => Task.FromResult(editor.Output);

    public Task<string> DumpAsync(CancellationToken cancellationToken) => editor.Client.DumpAsync(cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
