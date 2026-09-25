using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>The editor's state as the automation agent reports it (the "ready" signal).</summary>
/// <param name="MainWindowShown">The main window is open and visible.</param>
/// <param name="ProjectLoaded">A project is open and no project is loading.</param>
/// <param name="ReflectionLoaded">The types of the project's references are loaded.</param>
public sealed record AutomationStatus(bool MainWindowShown, bool ProjectLoaded, bool ReflectionLoaded, string? ProjectPath, int ProcessId);

/// <summary>One request line of the automation protocol.</summary>
public sealed record AutomationRequest(string Op)
{
    public AutomationQuery? Query { get; init; }
}

/// <summary>One response line of the automation protocol.</summary>
public sealed record AutomationResponse(bool Ok)
{
    public string? Error { get; init; }
    public IReadOnlyList<AutomationElement>? Elements { get; init; }
    public AutomationStatus? Status { get; init; }
    public string? Text { get; init; }
}

/// <summary>
/// Read-only automation endpoint of the desktop editor, enabled only with
/// <c>NETPRINTS_AUTOMATION=1</c>: a local pipe (a Unix domain socket on Linux) that answers
/// line-delimited JSON requests — <c>status</c> (the ready signal), <c>find</c> (elements by
/// automation id with screen bounds and properties), <c>dump</c> and <c>settle</c>. It never
/// changes the UI: tests send real input through the operating system (xdotool).
/// </summary>
public sealed class AutomationAgent : IDisposable
{
    public const string EnableVariable = "NETPRINTS_AUTOMATION";
    public const string PipeVariable = "NETPRINTS_AUTOMATION_PIPE";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string pipeName;
    private readonly AutomationTree tree;
    private readonly Func<AutomationStatus> status;
    private readonly CancellationTokenSource stop = new();

    public AutomationAgent(string pipeName, AutomationTree tree, Func<AutomationStatus> status)
    {
        this.pipeName = pipeName;
        this.tree = tree;
        this.status = status;
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Whether the environment asks for automation, and the pipe name to use.</summary>
    public static bool IsEnabled(out string pipeName)
    {
        pipeName = Environment.GetEnvironmentVariable(PipeVariable) is { Length: > 0 } name ? name : "netprints-automation";
        return Environment.GetEnvironmentVariable(EnableVariable) == "1";
    }

    private async Task AcceptLoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                return;
            }

            _ = Task.Run(() => ServeAsync(server));
        }
    }

    private async Task ServeAsync(NamedPipeServerStream stream)
    {
        await using var _ = stream;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        try
        {
            while (!stop.IsCancellationRequested && await reader.ReadLineAsync(stop.Token) is { } line)
            {
                AutomationResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<AutomationRequest>(line, Json) ?? throw new InvalidOperationException("Empty request.");
                    response = await HandleAsync(request);
                }
                catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
                {
                    response = new AutomationResponse(false) { Error = e.Message };
                }

                await writer.WriteLineAsync(JsonSerializer.Serialize(response, Json));
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The client went away or the editor is closing.
        }
    }

    private async Task<AutomationResponse> HandleAsync(AutomationRequest request)
    {
        switch (request.Op)
        {
            case "status":
                return new AutomationResponse(true) { Status = await Dispatcher.UIThread.InvokeAsync(status) };
            case "find":
                var query = request.Query ?? throw new ArgumentException("find needs a query.");
                return new AutomationResponse(true) { Elements = await Dispatcher.UIThread.InvokeAsync(() => tree.Find(query)) };
            case "dump":
                return new AutomationResponse(true) { Text = await Dispatcher.UIThread.InvokeAsync(tree.Dump) };
            case "settle":
                // Everything queued before this request, including layout, has run.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                return new AutomationResponse(true);
            default:
                throw new ArgumentException($"Unknown operation '{request.Op}'.");
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }
}
