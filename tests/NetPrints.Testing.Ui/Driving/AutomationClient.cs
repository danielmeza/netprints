using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;

namespace NetPrints.Testing.Ui.Driving;

/// <summary>Client of the editor's read-only automation agent (<see cref="AutomationAgent"/>).</summary>
public sealed class AutomationClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool outOfStep;

    private AutomationClient(NamedPipeClientStream pipe)
    {
        this.pipe = pipe;
        reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>Connects, retrying until the agent listens or the timeout ends.</summary>
    public static async Task<AutomationClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(timeout, cancellationToken);
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }

        return new AutomationClient(pipe);
    }

    public async Task<AutomationResponse> SendAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (outOfStep)
            {
                throw new IOException("An earlier request was cancelled or failed mid-exchange and may have left its reply in the pipe; open a new connection.");
            }

            string line;
            try
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(request, AutomationJsonContext.Default.AutomationRequest).AsMemory(), cancellationToken);
                line = await reader.ReadLineAsync(cancellationToken) ?? throw new IOException("The automation agent closed the connection.");
            }
            catch
            {
                outOfStep = true;
                throw;
            }

            var response = JsonSerializer.Deserialize(line, AutomationJsonContext.Default.AutomationResponse) ?? throw new IOException("Empty response.");
            return response.Ok ? response : throw new InvalidOperationException($"Automation request '{request.Op}' failed: {response.Error}");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AutomationElement>> FindAsync(AutomationQuery query, CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("find") { Query = query }, cancellationToken)).Elements ?? [];

    public async Task<AutomationStatus> StatusAsync(CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("status"), cancellationToken)).Status ?? throw new IOException("The reply carried no status.");

    public async Task<string> DumpAsync(CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("dump"), cancellationToken)).Text ?? "";

    /// <summary>Every window and every control with an automation id, hidden ones included.</summary>
    public async Task<IReadOnlyList<AutomationElement>> TreeAsync(CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("tree"), cancellationToken)).Elements ?? [];

    /// <summary>The state of the last program the editor launched.</summary>
    public async Task<RunStateSnapshot> RunStateAsync(CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("runState"), cancellationToken)).RunState ?? throw new IOException("The reply carried no run state.");

    public async Task SettleAsync(CancellationToken cancellationToken) => await SendAsync(new AutomationRequest("settle"), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        reader.Dispose();
        try
        {
            await writer.DisposeAsync();
        }
        catch (IOException)
        {
            // The agent is gone (the editor exited).
        }
        catch (ObjectDisposedException)
        {
            // Same.
        }

        await pipe.DisposeAsync();

        // Deliberately not gate.Dispose(): nothing here reads AvailableWaitHandle, so it has no
        // wait handle to release, and disposing it only made a racing SendAsync fault with an
        // ObjectDisposedException that could go unobserved (reported later, on an unrelated test).
        // The pipe close above already ends any in-flight call with an IOException.
    }
}
