using System.Diagnostics;
using System.Globalization;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// EditorApp's <c>desktop.ShutdownRequested</c> wiring (EditorApp.axaml.cs): closing the main
/// window awaits the host services' async cleanup exactly once, then the process exits. Rents its
/// own worker from <see cref="DesktopWorkerPool"/> (batch D2), so it runs independently of the
/// other smoke tests.
/// </summary>
public sealed class ShutdownTests(DesktopWorkerPool pool)
{
    /// <summary>The test's own work budget, starting once it has rented a worker (batch D3; see
    /// <see cref="X11SmokeTestBase.Timeout"/>).</summary>
    private const int Timeout = 60_000;

    [Fact]
    public async Task ClosingTheMainWindowDisposesHostServicesExactlyOnceThenExits()
    {
        if (!DesktopWorkerPool.IsEnabled)
        {
            Assert.Skip($"Desktop E2E tests run with {XServer.EnableVariable}=1 (Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3).");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var steps = new StepTimer(TestContext.Current.TestMethod?.MethodName ?? "test");
        string work = Directory.CreateTempSubdirectory("netprints-e2e-shutdown-").FullName;
        await using var lease = await pool.RentAsync(timeoutCts.Token, work);
        timeoutCts.CancelAfter(Timeout); // the budget starts now, not at dispatch (batch D3)
        var token = timeoutCts.Token;
        var tool = new Tool(lease.Server);
        var editor = lease.Editor;

        using (steps.Step("close window"))
        {
            string pid = editor.ProcessId.ToString(CultureInfo.InvariantCulture);
            string windows = await tool.XdotoolAsync(token, "search", "--all", "--onlyvisible", "--pid", pid);
            string windowId = windows.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First();
            await tool.XdotoolAsync(token, "windowclose", windowId);
        }

        using (steps.Step("wait exit"))
        {
            // Infrastructure wait for the process to exit: the automation pipe closes with it.
            var clock = Stopwatch.StartNew();
            while (!editor.HasExited && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(50, token);
            }
        }

        Assert.True(editor.HasExited, $"The editor did not exit after its main window was closed.\nStderr:\n{editor.Errors}");
        Assert.Equal(0, editor.ExitCode);
        Assert.Equal(1, editor.Errors.Split(EditorApp.ShutdownCleanupMarker, StringSplitOptions.None).Length - 1);

        try
        {
            Directory.Delete(work, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
