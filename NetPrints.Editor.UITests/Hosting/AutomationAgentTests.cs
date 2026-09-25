using Avalonia.Headless.XUnit;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>The desktop editor's automation agent answers over its pipe with what the automation tree sees.</summary>
public class AutomationAgentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnswersStatusFindDumpAndSettle()
    {
        using var app = HeadlessApp.Start();
        string pipe = Path.Combine(Path.GetTempPath(), "netprints-agent-" + Guid.NewGuid().ToString("N"));
        using var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));
        await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

        var status = await client.StatusAsync(Token);
        Assert.True(status.MainWindowShown);
        Assert.Equal(Environment.ProcessId, status.ProcessId);

        var buttons = await client.FindAsync(new AutomationQuery(AutomationIds.MainProjectButton), Token);
        var button = Assert.Single(buttons);
        Assert.Equal("Project", button.Text);
        Assert.Equal(100, button.Bounds.Width);
        Assert.True(button.IsEnabled);

        Assert.Contains(AutomationIds.MainRunButton, await client.DumpAsync(Token));
        await client.SettleAsync(Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(new AutomationRequest("click"), Token)); // read-only
    }
}
