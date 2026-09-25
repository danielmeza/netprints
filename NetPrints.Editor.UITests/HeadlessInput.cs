using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NetPrints.Editor.UITests;

/// <summary>
/// The headless driver: pointer and keyboard input, locating controls by automation id, and
/// condition-based waits. Page objects are the only callers that name automation ids.
/// </summary>
public static class HeadlessInput
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

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

    /// <summary>Waits until a condition holds; fails when the test is cancelled or times out.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what, int timeoutMs = 30_000)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(timeoutMs);

        while (!condition())
        {
            if (timeout.IsCancellationRequested)
            {
                Token.ThrowIfCancellationRequested();
                throw new TimeoutException($"Timed out waiting for: {what}");
            }

            Pump();
            await Task.Delay(10, Token);
        }

        Pump();
    }

    public static IEnumerable<T> Descendants<T>(this Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    /// <summary>All controls below <paramref name="root"/> (visual and logical, including popups) with an automation id.</summary>
    public static IEnumerable<T> AllById<T>(this Control root, string automationId) where T : Control =>
        root.GetVisualDescendants().OfType<T>()
            .Concat(root.GetLogicalDescendants().OfType<T>())
            .Distinct()
            .Where(c => AutomationProperties.GetAutomationId(c) == automationId);

    /// <summary>The single control with an automation id.</summary>
    public static T ById<T>(this Control root, string automationId) where T : Control
    {
        var matches = root.AllById<T>(automationId).ToList();
        Assert.True(matches.Count == 1, $"Expected one {typeof(T).Name} with automation id '{automationId}', found {matches.Count}.");
        return matches[0];
    }

    /// <summary>Center of a control in window coordinates.</summary>
    public static Point CenterIn(this Visual visual, Visual window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)
        ?? throw new InvalidOperationException("Control is not in the window.");

    public static void Click(this Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseMove(point);
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        Pump();
    }

    public static void DoubleClick(this Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    public static void Drag(this Window window, Point from, Point to, MouseButton button = MouseButton.Left, int steps = 10)
    {
        window.MouseMove(from);
        window.MouseDown(from, button);
        for (int i = 1; i <= steps; i++)
        {
            window.MouseMove(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
            Pump();
        }

        window.MouseUp(to, button);
        Pump();
    }

    public static void Wheel(this Window window, Point point, double delta)
    {
        window.MouseMove(point);
        window.MouseWheel(point, new Vector(0, delta));
        Pump();
    }

    public static void Press(this Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Pump();
    }

    public static void Type(this Window window, string text)
    {
        window.KeyTextInput(text);
        Pump();
    }
}
