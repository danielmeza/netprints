using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// Exceptions that escape to the UI thread (async void event handlers, unguarded awaits in async
/// commands) are shown in the error dialog instead of crashing the editor.
/// </summary>
public class UnhandledExceptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task WaitForErrorsAsync(ShellApp app, int count) =>
        UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Dialogs.Errors.Count == count), "error reported", Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DispatcherExceptionIsReportedAndTheEditorKeepsRunning()
    {
        await using var app = ShellApp.Start();

        Dispatcher.UIThread.Post(() => throw new InvalidOperationException("boom from the dispatcher"));
        await WaitForErrorsAsync(app, 1);

        Assert.Contains("boom from the dispatcher", app.Dialogs.Errors[0].Message);
        await new ShellPage(app.Driver).Menu.OpenAsync("File", ShellCommands.NewProject, Token); // still usable
        HeadlessDriver.DrainFinalizers();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AsyncVoidHandlerExceptionIsReported()
    {
        await using var app = ShellApp.Start();

        async void Handler()
        {
            await Task.Yield();
            throw new InvalidOperationException("boom from async void");
        }

        Dispatcher.UIThread.Post(Handler);
        await WaitForErrorsAsync(app, 1);

        Assert.Contains("boom from async void", app.Dialogs.Errors[0].Message);
        HeadlessDriver.DrainFinalizers(); // the carrying dispatcher task is finalized here, not in a later test
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AsyncVoidHandlerExceptionIsReportedOnlyOnce()
    {
        await using var app = ShellApp.Start();

        async void Handler()
        {
            await Task.Yield();
            throw new InvalidOperationException("boom once");
        }

        Dispatcher.UIThread.Post(Handler);
        await WaitForErrorsAsync(app, 1);

        // The dispatcher operation that carried the exception is finalized later; its unobserved
        // task must not report the same exception a second time.
        HeadlessDriver.DrainFinalizers();

        Assert.True(app.Dialogs.Errors.Count == 1, string.Join("\n---\n", app.Dialogs.Errors.Select(e => e.Message)));
    }
}
