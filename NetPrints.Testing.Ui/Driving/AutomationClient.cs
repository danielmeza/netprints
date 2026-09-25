using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NetPrints.Editor.Hosting.Automation;

namespace NetPrints.Testing.Ui.Driving;

/// <summary>Client of the editor's read-only automation agent (<see cref="AutomationAgent"/>).</summary>
public sealed class AutomationClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly SemaphoreSlim gate = new(1, 1);

    private AutomationClient(NamedPipeClientStream pipe)
    {
        this.pipe = pipe;
        reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>Connects, retrying until the agent listens or the timeout ends.</summary>
    public static async Task<AutomationClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
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
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, AutomationAgent.Json).AsMemory(), cancellationToken);
            string line = await reader.ReadLineAsync(cancellationToken) ?? throw new IOException("The automation agent closed the connection.");
            var response = JsonSerializer.Deserialize<AutomationResponse>(line, AutomationAgent.Json) ?? throw new IOException("Empty response.");
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
        (await SendAsync(new AutomationRequest("status"), cancellationToken)).Status!;

    public async Task<string> DumpAsync(CancellationToken cancellationToken) =>
        (await SendAsync(new AutomationRequest("dump"), cancellationToken)).Text ?? "";

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
        gate.Dispose();
    }
}
