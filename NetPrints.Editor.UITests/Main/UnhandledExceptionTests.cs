using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Main;

/// <summary>
/// Exceptions that escape to the UI thread (async void event handlers, unguarded awaits in async
/// commands) are shown in the error dialog instead of crashing the editor.
/// </summary>
public class UnhandledExceptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task WaitForErrorsAsync(HeadlessApp app, int count) =>
        UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Dialogs.Errors.Count == count), "error reported", Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DispatcherExceptionIsReportedAndTheEditorKeepsRunning()
    {
        using var app = HeadlessApp.Start();

        Dispatcher.UIThread.Post(() => throw new InvalidOperationException("boom from the dispatcher"));
        await WaitForErrorsAsync(app, 1);

        Assert.Contains("boom from the dispatcher", app.Dialogs.Errors[0].Message);
        await app.Main.ProjectButton.ClickAsync(Token);
        Assert.True(await app.Main.ProjectPane.IsVisibleAsync(Token)); // still usable
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AsyncVoidHandlerExceptionIsReported()
    {
        using var app = HeadlessApp.Start();

        async void Handler()
        {
            await Task.Yield();
            throw new InvalidOperationException("boom from async void");
        }

        Dispatcher.UIThread.Post(Handler);
        await WaitForErrorsAsync(app, 1);

        Assert.Contains("boom from async void", app.Dialogs.Errors[0].Message);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AsyncVoidHandlerExceptionIsReportedOnlyOnce()
    {
        using var app = HeadlessApp.Start();

        async void Handler()
        {
            await Task.Yield();
            throw new InvalidOperationException("boom once");
        }

        Dispatcher.UIThread.Post(Handler);
        await WaitForErrorsAsync(app, 1);

        // The dispatcher operation that carried the exception is finalized later; its unobserved
        // task must not report the same exception a second time.
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            HeadlessDriver.Pump();
        }

        Assert.True(app.Dialogs.Errors.Count == 1, string.Join("\n---\n", app.Dialogs.Errors.Select(e => e.Message)));
    }
}
