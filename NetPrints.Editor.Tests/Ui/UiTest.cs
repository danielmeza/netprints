using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetPrints.Editor.Tests.Ui;

/// <summary>Builds the real <see cref="EditorApp"/> on Avalonia's headless platform with Skia rendering.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EditorApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            // No system fonts are needed (clean CI images and containers have none).
            .With(new FontManagerOptions { DefaultFamilyName = EditorApp.DefaultFontFamily });
}

/// <summary>
/// Runs code on the shared headless UI thread.
/// </summary>
/// <remarks>
/// Verified gotchas (research.md): an exception thrown inside <c>session.Dispatch</c> hangs the run,
/// so exceptions are captured inside and rethrown outside; disposing the session hangs with
/// Avalonia 12, so the session lives until the test process exits. Every UI test also has a
/// [Timeout] and the assembly runs tests sequentially.
/// </remarks>
public static class UiTest
{
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder)), LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task RunAsync(Action action) => RunAsync(() =>
    {
        action();
        return Task.CompletedTask;
    });

    public static async Task RunAsync(Func<Task> action)
    {
        ExceptionDispatchInfo? error = null;

        // The Func<Task<T>> overload is required: with a plain async lambda the compiler picks an
        // overload that does not await the body, and the test would finish before its assertions.
        Func<Task<bool>> body = async () =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }

            return true;
        };

        bool completed = await Session.Value.Dispatch(body, CancellationToken.None);
        if (!completed)
        {
            throw new InvalidOperationException("The UI test body did not complete.");
        }

        error?.Throw();
    }

    /// <summary>
    /// Processes pending UI work (layout, bindings, posted actions) and renders a frame, so that
    /// hit testing (which uses the render scene in Avalonia 12) sees the current layout.
    /// </summary>
    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Waits on the UI thread until a condition holds, pumping the dispatcher.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 30000, string? message = null)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(message ?? "Condition was not reached in time.");
            }

            Pump();
            await Task.Delay(10);
        }

        Pump();
    }
}
