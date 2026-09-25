using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>The desktop editor's automation agent answers over its pipe with what the automation tree sees.</summary>
public class AutomationAgentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NewPipeName() => Path.Combine(Path.GetTempPath(), "netprints-agent-" + Guid.NewGuid().ToString("N"));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnswersStatusFindDumpAndSettle()
    {
        using var app = HeadlessApp.Start();
        string pipe = NewPipeName();
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

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RejectsAQueryWithNoAutomationId()
    {
        using var app = HeadlessApp.Start();
        string pipe = NewPipeName();
        using var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));
        await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

        // {"op":"find","query":{}} deserializes with AutomationId == null: it must not match every
        // unnamed control.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.FindAsync(new AutomationQuery(""), Token));
        Assert.Contains("automation id", ex.Message);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DropsAConnectionThatSendsAnOversizedRequestLine()
    {
        using var app = HeadlessApp.Start();
        string pipe = NewPipeName();
        using var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));

        await using var raw = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await raw.ConnectAsync(TimeSpan.FromSeconds(10), Token);
        using var reader = new StreamReader(raw, Encoding.UTF8, false, 4096, leaveOpen: true);
        var writer = new StreamWriter(raw, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
        try
        {
            // Well over the agent's 64 KiB line cap, and never terminated: a well-behaved client
            // couldn't produce this, only a hostile or broken one.
            await writer.WriteAsync(new string('a', 200_000));
            await writer.WriteAsync('\n');
        }
        catch (IOException)
        {
            // The agent may have already dropped the connection by the time the write completes.
        }
        finally
        {
            TryDispose(writer);
        }

        // The agent drops the connection instead of answering or growing the buffer without bound:
        // a clean EOF, or a reset if the OS still had unread bytes queued when it closed its end.
        await AssertConnectionDroppedAsync(reader);
    }

    /// <summary>Asserts the peer closed the connection: a clean EOF, or a reset for unread bytes.</summary>
    private static async Task AssertConnectionDroppedAsync(StreamReader reader)
    {
        try
        {
            Assert.Null(await reader.ReadLineAsync(Token));
        }
        catch (IOException)
        {
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RefusesConnectionsBeyondTheConcurrencyCap()
    {
        using var app = HeadlessApp.Start();
        string pipe = NewPipeName();
        using var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));

        // One over the agent's cap: connects at the transport level (the OS accepts it), but the
        // agent refuses to serve it, so it sees no bytes back and the pipe closes. A status round
        // trip on each of the first 8 proves its slot is actually held (not just connecting)
        // before the 9th is attempted, so the test does not race the agent's own bookkeeping.
        var clients = new List<AutomationClient>();
        try
        {
            for (int i = 0; i < 8; i++)
            {
                var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);
                await client.StatusAsync(Token);
                clients.Add(client);
            }

            await using var extra = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await extra.ConnectAsync(TimeSpan.FromSeconds(10), Token);
            using var reader = new StreamReader(extra, Encoding.UTF8, false, 4096, leaveOpen: true);
            var writer = new StreamWriter(extra, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            try
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(new AutomationRequest("status"), AutomationAgent.Json));
            }
            catch (IOException)
            {
                // The agent may have already dropped the connection by the time the write completes.
            }
            finally
            {
                TryDispose(writer);
            }

            await AssertConnectionDroppedAsync(reader);
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    /// <summary>Disposes a writer over a pipe that may already be broken (the point of the test).</summary>
    private static void TryDispose(StreamWriter writer)
    {
        try
        {
            writer.Dispose();
        }
        catch (IOException)
        {
        }
    }
}
