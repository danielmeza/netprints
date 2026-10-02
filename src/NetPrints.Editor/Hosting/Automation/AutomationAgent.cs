using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>The editor's state as the automation agent reports it (the "ready" signal).</summary>
/// <param name="MainWindowShown">The main window is open and visible.</param>
/// <param name="ProjectLoaded">A project is open and no project is loading.</param>
/// <param name="ReflectionLoaded">The types of the project's references are loaded.</param>
/// <param name="ProjectPath">Path to the open project, or <see langword="null"/> if none is open.</param>
/// <param name="ProcessId">The editor process's id.</param>
public sealed record AutomationStatus(bool MainWindowShown, bool ProjectLoaded, bool ReflectionLoaded, string? ProjectPath, int ProcessId);

/// <summary>One request line of the automation protocol.</summary>
public sealed record AutomationRequest(string Op)
{
    /// <summary>The query for a <c>find</c> request; required for that operation, unused otherwise.</summary>
    public AutomationQuery? Query { get; init; }
}

/// <summary>One response line of the automation protocol.</summary>
public sealed record AutomationResponse(bool Ok)
{
    /// <summary>The exception message, when <see cref="Ok"/> is <see langword="false"/>.</summary>
    public string? Error { get; init; }

    /// <summary>The matched elements, for a <c>find</c> request.</summary>
    public IReadOnlyList<AutomationElement>? Elements { get; init; }

    /// <summary>The editor's ready-signal snapshot, for a <c>status</c> request.</summary>
    public AutomationStatus? Status { get; init; }

    /// <summary>The last launched program's state, for a <c>runState</c> request.</summary>
    public RunStateSnapshot? RunState { get; init; }

    /// <summary>The window/element tree dump, for a <c>dump</c> request.</summary>
    public string? Text { get; init; }
}

/// <summary>A client sent a request line the agent will not read further (too long).</summary>
public sealed class AutomationProtocolException(string message) : IOException(message);

/// <summary>
/// Read-only automation endpoint of the desktop editor, enabled only with
/// <c>NETPRINTS_AUTOMATION=1</c>: a local pipe (a Unix domain socket on Linux) that answers
/// line-delimited JSON requests — <c>status</c> (the ready signal), <c>find</c> (elements by
/// automation id with screen bounds and properties), <c>dump</c>, <c>tree</c> (every window and
/// every control with an automation id) and <c>runState</c> (the last launched program), both for
/// failure diagnostics, and <c>settle</c>. It never
/// changes the UI: tests send real input through the operating system (xdotool).
///
/// The pipe is current-user-only (<see cref="PipeOptions.CurrentUserOnly"/> on both ends) and, by
/// default, per-user and per-process: under <c>XDG_RUNTIME_DIR</c> (a 0700 directory) on Unix, or
/// a name that includes the user and process id on Windows. <see cref="PipeVariable"/> overrides
/// the default for callers that need a known path (the E2E test harness).
/// </summary>
public sealed class AutomationAgent : IDisposable
{
    /// <summary>
    /// Environment variable that enables the automation agent when set to "1".
    /// </summary>
    public const string EnableVariable = "NETPRINTS_AUTOMATION";

    /// <summary>
    /// Environment variable that overrides the automation pipe's name/path (see
    /// <see cref="DefaultPipeName"/> for the default), for callers that need a known path.
    /// </summary>
    public const string PipeVariable = "NETPRINTS_AUTOMATION_PIPE";

    /// <summary>Longest request line the agent reads before dropping the connection.</summary>
    private const int MaxLineChars = 64 * 1024;

    /// <summary>Most automation connections served at once; the rest are refused immediately.</summary>
    private const int MaxConcurrentConnections = 8;

    private readonly string pipeName;
    private readonly AutomationTree tree;
    private readonly Func<AutomationStatus> status;
    private readonly Func<RunStateSnapshot>? runState;
    private readonly ILogger<AutomationAgent> logger;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim connectionSlots = new(MaxConcurrentConnections, MaxConcurrentConnections);
    private NamedPipeServerStream nextServer;

    /// <summary>
    /// Binds the pipe. Failures (a socket path over the Unix 108-character limit, another process
    /// already owning the name, …) throw synchronously from here, so the caller can log them and
    /// fail fast instead of the bind happening inside a background task nobody observes.
    /// </summary>
    public AutomationAgent(string pipeName, AutomationTree tree, Func<AutomationStatus> status, ILogger<AutomationAgent> logger, Func<RunStateSnapshot>? runState = null)
    {
        ArgumentNullException.ThrowIfNull(pipeName);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(logger);

        this.pipeName = pipeName;
        this.tree = tree;
        this.status = status;
        this.runState = runState;
        this.logger = logger;
        nextServer = CreateServer();
        Task.Run(AcceptLoopAsync).Forget(e => LogError("accept loop task fault", e));
    }

    /// <summary>Whether the environment asks for automation, and the pipe name to use.</summary>
    public static bool IsEnabled(out string pipeName)
    {
        pipeName = Environment.GetEnvironmentVariable(PipeVariable) is { Length: > 0 } name ? name : DefaultPipeName();
        return Environment.GetEnvironmentVariable(EnableVariable) == "1";
    }

    /// <summary>
    /// A per-user, per-process default that nobody else can guess ahead of time or squat: on
    /// Unix, a path under <c>XDG_RUNTIME_DIR</c> (a 0700 directory owned by the user, falling back
    /// to the shared temp directory only if that variable is unset); on Windows, a name that
    /// includes the user and process id (named pipes there are already namespaced per session).
    /// </summary>
    private static string DefaultPipeName()
    {
        if (OperatingSystem.IsWindows())
        {
            return $"netprints-automation-{Environment.UserName}-{Environment.ProcessId}";
        }

        string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } xdg ? xdg : Path.GetTempPath();
        return Path.Combine(dir, $"netprints-automation-{Environment.ProcessId}.sock");
    }

    private NamedPipeServerStream CreateServer() =>
        new(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task AcceptLoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var server = nextServer;
            try
            {
                await server.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception e)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                LogError("stopped accepting connections", e);
                return;
            }

            if (await connectionSlots.WaitAsync(0).ConfigureAwait(false))
            {
                ServeConnectionAsync(server).Forget(e => LogError("unexpected connection task fault", e));
            }
            else
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }

            try
            {
                nextServer = CreateServer();
            }
            catch (Exception e)
            {
                LogError("stopped accepting connections", e);
                return;
            }
        }
    }

    /// <summary>
    /// Runs one connection to completion and releases its slot. <see cref="ServeAsync"/> already
    /// turns every request-handling exception into a response and every I/O exception into a
    /// quiet disconnect, so nothing should reach the catch below; it exists so that if something
    /// unforeseen does, it is logged instead of becoming an unobserved task exception (and the
    /// modal error dialog that comes with one).
    /// </summary>
    private async Task ServeConnectionAsync(NamedPipeServerStream stream)
    {
        try
        {
            await ServeAsync(stream).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogError("connection error", e);
        }
        finally
        {
            connectionSlots.Release();
        }
    }

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP007", Justification = "ADR-0003: ServeAsync takes ownership of its connection stream.")]
    private async Task ServeAsync(NamedPipeServerStream stream)
    {
        await using var _ = stream;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        var lines = new BoundedLineReader(reader, MaxLineChars);

        try
        {
            while (!stop.IsCancellationRequested && await lines.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line)
            {
                AutomationResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize(line, AutomationJsonContext.Default.AutomationRequest) ?? throw new InvalidOperationException("Empty request.");
                    response = await HandleAsync(request).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    response = new AutomationResponse(false) { Error = e.Message };
                }

                await writer.WriteLineAsync(JsonSerializer.Serialize(response, AutomationJsonContext.Default.AutomationResponse)).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The client went away, the editor is closing, or it sent an oversized request line
            // (AutomationProtocolException derives from IOException).
        }
    }

    private async Task<AutomationResponse> HandleAsync(AutomationRequest request)
    {
        switch (request.Op)
        {
            case AutomationOps.Status:
                return new AutomationResponse(true) { Status = await Dispatcher.UIThread.InvokeAsync(status) };
            case AutomationOps.Find:
                var query = request.Query ?? throw new ArgumentException("find needs a query.");
                ValidateQuery(query);
                return new AutomationResponse(true) { Elements = await Dispatcher.UIThread.InvokeAsync(() => tree.Find(query)) };
            case AutomationOps.Dump:
                return new AutomationResponse(true) { Text = await Dispatcher.UIThread.InvokeAsync(tree.Dump) };
            case AutomationOps.RunState:
                var provider = runState ?? throw new InvalidOperationException("This agent has no run state.");
                return new AutomationResponse(true) { RunState = provider() };
            case AutomationOps.Tree:
                return new AutomationResponse(true) { Elements = await Dispatcher.UIThread.InvokeAsync(tree.Snapshot) };
            case AutomationOps.Settle:
                // Everything queued before this request, including layout, has run.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                return new AutomationResponse(true);
            default:
                throw new ArgumentException($"Unknown operation '{request.Op}'.");
        }
    }

    /// <summary>
    /// Rejects a query (or an enclosing one) with no automation id: unset, it matches every
    /// unnamed control, and each match is then described and measured for text overflow.
    /// </summary>
    private static void ValidateQuery(AutomationQuery query)
    {
        if (string.IsNullOrEmpty(query.AutomationId))
        {
            throw new ArgumentException("A query needs a non-empty automation id.");
        }

        if (query.Within is { } within)
        {
            ValidateQuery(within);
        }
    }

    private void LogError(string what, Exception e) => Log.AutomationAgentError(logger, e, pipeName, what);

    /// <summary>
    /// Stops accepting connections and releases the pipe. In-flight connections are not forcibly
    /// closed; they end on their own once the client disconnects or the process exits.
    /// </summary>
    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();

        // Whatever server is sitting unconsumed (the accept loop's cancellation check runs before
        // it picks up the next one) would otherwise leak its socket.
        nextServer.Dispose();

        // Deliberately not connectionSlots.Dispose(): nothing here reads AvailableWaitHandle, so
        // it has no wait handle to release, and disposing it only made a racing
        // ServeConnectionAsync's finally -> Release() fault with an ObjectDisposedException that
        // went unobserved (fire-and-forget), reported later against an unrelated test.
    }
}

/// <summary>
/// Reads lines from a <see cref="TextReader"/>, capping the characters read before a newline so a
/// client that never sends one cannot grow the buffer without bound.
/// </summary>
file sealed class BoundedLineReader(TextReader reader, int maxChars)
{
    private readonly char[] buffer = new char[4096];
    private int start;
    private int length;

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (start >= length)
            {
                length = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                start = 0;
                if (length == 0)
                {
                    return line.Length == 0 ? null : line.ToString();
                }
            }

            int newline = Array.IndexOf(buffer, '\n', start, length - start);
            int end = newline == -1 ? length : newline;
            int count = end - start;
            if (line.Length + count > maxChars)
            {
                throw new AutomationProtocolException($"Request line exceeds {maxChars} characters.");
            }

            line.Append(buffer, start, count);
            start = newline == -1 ? length : newline + 1;

            if (newline != -1)
            {
                if (line.Length > 0 && line[^1] == '\r')
                {
                    line.Length--;
                }

                return line.ToString();
            }
        }
    }
}
