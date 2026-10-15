using Avalonia.Headless.XUnit;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>A saved layout the adapter cannot use is logged by the composed editor (ADR-0018), not dropped silently.</summary>
public class LayoutFallbackLoggingTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ALayoutForAnotherEngineIsLoggedAndTheDefaultLayoutIsUsed()
    {
        var store = new MemoryStateStore { Layout = new LayoutState(StateFile.CurrentVersion, "grid", null) };
        var logs = new RecordingLoggerFactory();

        await using ShellApp app = ShellApp.Start(store, logs);

        Assert.Contains(1210, logs.Ids);
        Assert.NotEmpty(app.Shell.Panels);
    }
}
