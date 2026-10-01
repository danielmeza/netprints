using System.Globalization;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// ADR-0018 spike, check 4 (throwaway): floating and re-docking a pane under Xvfb and openbox, against the spike window the
/// desktop host opens with <c>NETPRINTS_DOCK_SPIKE=1</c>. The test drives the float and dock buttons, not a drag.
/// </summary>
public sealed class DockSpikeTests(DesktopWorkerPool pool)
{
    private const int Timeout = 120_000;
    private const string FloatingTitle = "A#2";
    private static readonly IReadOnlyDictionary<string, string> SpikeEnvironment = new Dictionary<string, string> { ["NETPRINTS_DOCK_SPIKE"] = "1" };

    private static AutomationQuery ContentId(string text) => new(AutomationIds.DockSpikeContentId) { Text = text };

    [Fact]
    public async Task APaneFloatsIntoItsOwnWindowAndDocksBack()
    {
        if (!DesktopWorkerPool.IsEnabled)
        {
            Assert.Skip($"Desktop E2E tests run with {XServer.EnableVariable}=1 (Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3).");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string work = Directory.CreateTempSubdirectory("netprints-e2e-dockspike-").FullName;
        await using var lease = await pool.RentAsync(timeoutCts.Token, work, SpikeEnvironment);
        timeoutCts.CancelAfter(Timeout);
        var token = timeoutCts.Token;
        var tool = new Tool(lease.Server);
        var client = lease.Editor.Client;
        var driver = new NetPrints.Desktop.E2ETests.Driving.X11Driver(lease.Server, lease.Editor, tool);
        string pid = lease.Editor.ProcessId.ToString(CultureInfo.InvariantCulture);
        string artifacts = Path.Combine(
            Environment.GetEnvironmentVariable(NetPrints.Testing.TestEnvironment.UiArtifactsVariable) is { Length: > 0 } configured ? configured : Path.Combine(AppContext.BaseDirectory, "ui-artifacts"),
            "e2e", "dock-spike");
        Directory.CreateDirectory(artifacts);

        try
        {
            await client.SettleAsync(token);
            var floatButton = Assert.Single(await client.FindAsync(new AutomationQuery(AutomationIds.DockSpikeFloat), token));
            var dockButton = Assert.Single(await client.FindAsync(new AutomationQuery(AutomationIds.DockSpikeDock), token));
            string spikeKey = floatButton.Window;
            Assert.Equal(spikeKey, Assert.Single(await client.FindAsync(ContentId("NPT-A"), token)).Window);
            Assert.Equal(spikeKey, Assert.Single(await client.FindAsync(ContentId("NPT-TOOL"), token)).Window);
            Assert.Empty(await client.FindAsync(ContentId("NPT-B"), token));
            Assert.Empty(await Windows(tool, token, "--name", FloatingTitle));

            // Float: a second top-level window appears on the display and holds the content.
            await Click(tool, floatButton, client, token);
            var floating = await WaitForSingleAsync(client, ContentId("NPT-B"), token);
            Assert.NotEqual(spikeKey, floating.Window);
            Assert.Equal(spikeKey, Assert.Single(await client.FindAsync(ContentId("NPT-A"), token)).Window);
            string[] floatingWindows = await Windows(tool, token, "--onlyvisible", "--name", FloatingTitle);
            string nativeWindow = Assert.Single(floatingWindows);
            Assert.Equal(nativeWindow, floating[AutomationPropertyNames.X11Window]);
            Assert.Contains("Map State: IsViewable", await tool.RunAsync("xwininfo", token, "-id", nativeWindow), StringComparison.Ordinal);
            (await driver.ScreenAsync(token)).Save(Path.Combine(artifacts, "floating.png"));

            // Re-dock: the content is back in the spike window and the native window is gone.
            await Click(tool, dockButton, client, token);
            var docked = await WaitForSingleAsync(client, ContentId("NPT-B"), token);
            Assert.Equal(spikeKey, docked.Window);
            Assert.Empty(await Windows(tool, token, "--name", FloatingTitle));
            (await driver.ScreenAsync(token)).Save(Path.Combine(artifacts, "redocked.png"));
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(artifacts, "editor-stderr.txt"), lease.Editor.Errors, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(artifacts, "xdotool.txt"), tool.Log, CancellationToken.None);
            Directory.Delete(work, recursive: true);
        }
    }

    private static async Task Click(Tool tool, AutomationElement element, NetPrints.Testing.Ui.Driving.AutomationClient client, CancellationToken token)
    {
        string x = ((int)Math.Round(element.ScreenBounds.CenterX)).ToString(CultureInfo.InvariantCulture);
        string y = ((int)Math.Round(element.ScreenBounds.CenterY)).ToString(CultureInfo.InvariantCulture);
        await tool.XdotoolAsync(token, "mousemove", "--sync", x, y);
        await tool.XdotoolAsync(token, "click", "1");
        await client.SettleAsync(token);
    }

    /// <summary>Waits for the editor to show exactly one match: an infrastructure wait on the window manager mapping a new window.</summary>
    private static async Task<AutomationElement> WaitForSingleAsync(NetPrints.Testing.Ui.Driving.AutomationClient client, AutomationQuery query, CancellationToken token)
    {
        while (true)
        {
            await client.SettleAsync(token);
            var found = await client.FindAsync(query, token);
            if (found.Count == 1)
            {
                return found[0];
            }

            await Task.Delay(100, token);
        }
    }

    private static async Task<string[]> Windows(Tool tool, CancellationToken token, params string[] options)
    {
        var (code, output) = await tool.TryRunAsync("xdotool", token, ["search", .. options]);
        return code == 0 ? output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
    }
}
