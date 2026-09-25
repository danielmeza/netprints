using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace NetPrints.Editor.UITests.Main;

/// <summary>
/// Exceptions that escape to the UI thread (async void event handlers, unguarded awaits in async
/// commands) are shown in the error dialog instead of crashing the editor.
/// </summary>
public class UnhandledExceptionTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DispatcherExceptionIsReportedAndTheEditorKeepsRunning()
    {
        using var main = MainWindowPage.Start();

        Dispatcher.UIThread.Post(() => throw new InvalidOperationException("boom from the dispatcher"));
        await HeadlessInput.WaitUntilAsync(() => main.Dialogs.Errors.Count == 1, "error reported");

        Assert.Contains("boom from the dispatcher", main.Dialogs.Errors[0].Message);
        main.ClickProject();
        Assert.True(main.IsProjectPaneVisible); // still usable
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AsyncVoidHandlerExceptionIsReported()
    {
        using var main = MainWindowPage.Start();

        async void Handler()
        {
            await Task.Yield();
            throw new InvalidOperationException("boom from async void");
        }

        Dispatcher.UIThread.Post(Handler);
        await HeadlessInput.WaitUntilAsync(() => main.Dialogs.Errors.Count == 1, "error reported");

        Assert.Contains("boom from async void", main.Dialogs.Errors[0].Message);
    }
}
