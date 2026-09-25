using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;
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

    /// <summary>
    /// Disposing an <see cref="AutomationClient"/> while a request is still in flight must not
    /// leave an unobserved task exception behind (it used to: DisposeAsync's gate.Dispose() could
    /// fault a racing SendAsync with ObjectDisposedException("SemaphoreSlim"), and nothing awaited
    /// that particular call, so the GC finalizer thread reported it minutes later, on whatever
    /// unrelated test happened to be running then — the flake this test guards against).
    /// </summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DisposingWhileARequestIsInFlightFaultsOnlyThatRequest()
    {
        using var app = HeadlessApp.Start();

        await AssertNoUnobservedExceptionsAsync(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                string pipe = NewPipeName();
                using var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));
                var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

                // Started but not awaited before disposing, to race SendAsync's gate against
                // DisposeAsync as closely as this process can arrange without a sleep.
                var pending = client.StatusAsync(Token);
                await client.DisposeAsync();

                try
                {
                    await pending;
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
                {
                    // Expected: the pipe closed under it. Must not be the gate (see DisposeAsync).
                    Assert.DoesNotContain("SemaphoreSlim", ex.Message);
                }
            }
        });
    }

    /// <summary>
    /// Disposing the <see cref="AutomationAgent"/> itself while a connection is being served must
    /// not leave an unobserved task exception behind either: Dispose() used to dispose
    /// connectionSlots synchronously while ServeConnectionAsync's finally, running asynchronously
    /// on the connection's own task, was still going to call connectionSlots.Release() — an
    /// ObjectDisposedException("SemaphoreSlim") from a fire-and-forget task nobody awaited.
    /// Reproduced 300/300 by the reviewer against the built assembly.
    /// </summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DisposingTheAgentWhileAConnectionIsServedDoesNotFaultItsRelease()
    {
        using var app = HeadlessApp.Start();

        await AssertNoUnobservedExceptionsAsync(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                string pipe = NewPipeName();
                var agent = new AutomationAgent(pipe, app.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId));
                await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

                // A round trip proves the connection is actually being served (its slot held,
                // ServeAsync now blocked reading the next line) before racing it against Dispose.
                await client.StatusAsync(Token);
                agent.Dispose();
            }
        });
    }

    /// <summary>
    /// A faulted task left unobserved by earlier work (another test) must not be blamed on the
    /// code under test: the check used to catch it when the GC finalized it during the check.
    /// </summary>
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheUnobservedExceptionCheckIgnoresEarlierFaultedTasks()
    {
        LeaveAnUnobservedFaultedTask();

        await AssertNoUnobservedExceptionsAsync(() => Task.CompletedTask);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void LeaveAnUnobservedFaultedTask() => _ = Task.FromException(new InvalidOperationException("from earlier work"));

    /// <summary>
    /// Drains finalizers left by earlier work, runs <paramref name="actAsync"/>, then forces pending finalizers to run, and asserts none of
    /// it produced an unobserved task exception. An exception this test itself already awaited is
    /// not "unobserved" (TaskScheduler.UnobservedTaskException never fires for it), so this only
    /// catches exactly the class of bug these dispose-race tests guard against.
    /// </summary>
    private static async Task AssertNoUnobservedExceptionsAsync(Func<Task> actAsync)
    {
        Exception? unobserved = null;
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            unobserved ??= e.Exception.Flatten().InnerExceptions.FirstOrDefault();
        }

        HeadlessDriver.DrainFinalizers(); // faulted tasks of earlier tests surface now, before the handler is attached
        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            await actAsync();
            HeadlessDriver.DrainFinalizers();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        Assert.Null(unobserved);
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
