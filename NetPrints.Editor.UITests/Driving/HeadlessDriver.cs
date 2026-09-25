using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Driving;

/// <summary>
/// <see cref="IUiDriver"/> on Avalonia's headless platform: elements come from the same
/// <see cref="AutomationTree"/> the in-app agent uses, input is raw headless mouse and keyboard
/// input, screenshots are rendered frames. Runs on the headless UI thread.
/// </summary>
public sealed class HeadlessDriver(AutomationTree tree, Func<string> programOutput) : IUiDriver
{
    private Window? keyboardWindow;

    public string Name => "headless";

    public UiCapabilities Capabilities => UiCapabilities.ProcessOutput;

    /// <summary>
    /// Processes pending UI work and renders a frame, so that hit testing (which uses the render
    /// scene in Avalonia 12) sees the current layout.
    /// </summary>
    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public Task<IReadOnlyList<AutomationElement>> FindAllAsync(AutomationQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(tree.Find(query));
    }

    public UiTarget At(AutomationElement element, double fractionX = 0.5, double fractionY = 0.5) =>
        new(element.Window, element.Bounds.X + element.Bounds.Width * fractionX, element.Bounds.Y + element.Bounds.Height * fractionY);

    public Task SettleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Pump();
        return Task.CompletedTask;
    }

    private Window WindowOf(UiTarget target)
    {
        var window = tree.WindowByKey(target.Window);
        keyboardWindow = window;
        return window;
    }

    private static Point P(UiTarget target) => new(target.X, target.Y);

    private static MouseButton Map(UiButton button) => button switch
    {
        UiButton.Left => MouseButton.Left,
        UiButton.Middle => MouseButton.Middle,
        UiButton.Right => MouseButton.Right,
        UiButton.Back => MouseButton.XButton1,
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private static RawInputModifiers Held(UiButton button) => button switch
    {
        UiButton.Left => RawInputModifiers.LeftMouseButton,
        UiButton.Middle => RawInputModifiers.MiddleMouseButton,
        UiButton.Right => RawInputModifiers.RightMouseButton,
        UiButton.Back => RawInputModifiers.XButton1MouseButton,
        _ => RawInputModifiers.None,
    };

    public Task MoveAsync(UiTarget target, CancellationToken cancellationToken)
    {
        WindowOf(target).MouseMove(P(target));
        Pump();
        return Task.CompletedTask;
    }

    public Task ClickAsync(UiTarget target, UiButton button, int clickCount, CancellationToken cancellationToken)
    {
        var window = WindowOf(target);
        window.MouseMove(P(target));
        for (int i = 0; i < clickCount; i++)
        {
            window.MouseDown(P(target), Map(button));
            window.MouseUp(P(target), Map(button));
        }

        Pump();
        return Task.CompletedTask;
    }

    public async Task DragAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken)
    {
        await PressAndMoveAsync(from, to, button, cancellationToken);
        await ReleaseAsync(to, button, cancellationToken);
    }

    public Task PressAndMoveAsync(UiTarget from, UiTarget to, UiButton button, CancellationToken cancellationToken)
    {
        const int steps = 10;
        var window = WindowOf(from);
        window.MouseMove(P(from));
        window.MouseDown(P(from), Map(button));
        for (int i = 1; i <= steps; i++)
        {
            // Moves report the held button, as a real pointer does.
            window.MouseMove(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps), Held(button));
            Pump();
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(UiTarget at, UiButton button, CancellationToken cancellationToken)
    {
        WindowOf(at).MouseUp(P(at), Map(button));
        Pump();
        return Task.CompletedTask;
    }

    public Task WheelAsync(UiTarget target, double delta, CancellationToken cancellationToken)
    {
        var window = WindowOf(target);
        window.MouseMove(P(target));
        window.MouseWheel(P(target), new Vector(0, delta));
        Pump();
        return Task.CompletedTask;
    }

    private Window KeyboardWindow =>
        keyboardWindow is { IsVisible: true } ? keyboardWindow
        : tree.Windows.LastOrDefault(w => w.IsActive) ?? tree.Windows.LastOrDefault() ?? throw new InvalidOperationException("No open window.");

    public Task TypeAsync(string text, CancellationToken cancellationToken)
    {
        KeyboardWindow.KeyTextInput(text);
        Pump();
        return Task.CompletedTask;
    }

    public Task PressAsync(string chord, CancellationToken cancellationToken)
    {
        var (key, modifiers) = ParseChord(chord);
        var window = KeyboardWindow;
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Pump();
        return Task.CompletedTask;
    }

    /// <summary>Parses "Ctrl+Shift+Z", "Delete", "Enter" …</summary>
    public static (Key Key, RawInputModifiers Modifiers) ParseChord(string chord)
    {
        var modifiers = RawInputModifiers.None;
        var parts = chord.Split('+');
        foreach (string part in parts[..^1])
        {
            modifiers |= part switch
            {
                "Ctrl" => RawInputModifiers.Control,
                "Shift" => RawInputModifiers.Shift,
                "Alt" => RawInputModifiers.Alt,
                _ => throw new ArgumentException($"Unknown modifier {part} in {chord}."),
            };
        }

        string name = parts[^1] switch
        {
            "Esc" => nameof(Key.Escape),
            var s when s.Length == 1 && char.IsDigit(s[0]) => "D" + s,
            var s => s,
        };
        return (Enum.Parse<Key>(name, ignoreCase: true), modifiers);
    }

    /// <summary>
    /// Simulates an operating-system drop of <paramref name="data"/> at a point (enter, over, drop);
    /// the headless platform has no drag source, so the payload is arranged by the test.
    /// </summary>
    public void Drop(UiTarget at, IDataTransfer data)
    {
        var window = WindowOf(at);
        window.DragDrop(P(at), RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(P(at), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(P(at), RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        Pump();
    }

    public Task<UiImage> ScreenshotAsync(string window, CancellationToken cancellationToken)
    {
        Pump();
        using var frame = tree.WindowByKey(window).CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
        using var stream = new MemoryStream();
        frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        return Task.FromResult(new UiImage(stream.ToArray()));
    }

    public Task<string?> CursorNameAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The headless platform has no pointer cursor; read the Cursor property instead.");

    public Task MinimizeAsync(string window, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The headless platform has no window manager.");

    public Task<bool> IsMinimizedAsync(string window, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The headless platform has no window manager.");

    public Task<string> ProgramOutputAsync(CancellationToken cancellationToken) => Task.FromResult(programOutput());

    public Task<string> DumpAsync(CancellationToken cancellationToken) => Task.FromResult(tree.Dump());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
