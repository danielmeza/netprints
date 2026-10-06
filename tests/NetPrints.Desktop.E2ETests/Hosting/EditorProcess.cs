using System.Diagnostics;
using System.Reflection;
using System.Text;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// The real desktop editor (NetPrints.Desktop) on the private display, in automation mode, with
/// its console output captured (programs it runs inherit it).
/// </summary>
public sealed class EditorProcess : ICapturedEditor, IAsyncDisposable
{
    /// <summary>The <c>sockaddr_un</c> path length limit on Linux; a longer path fails to bind.</summary>
    private const int UnixSocketPathLimit = 108;

    private readonly Process process;
    private readonly StringBuilder output = new();
    private readonly StringBuilder errors = new();

    private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private EditorProcess(Process process, AutomationClient client, string pipeName)
    {
        this.process = process;
        PipeName = pipeName;
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => exited.TrySetResult();
        if (process.HasExited)
        {
            exited.TrySetResult();
        }

        Client = client;
    }

    public AutomationClient Client { get; }

    /// <summary>The automation pipe; the agent accepts several connections, so diagnostics can use their own.</summary>
    public string PipeName { get; }

    /// <summary>Opens a connection of its own, so a request left in flight on <see cref="Client"/> cannot put it out of step.</summary>
    public Task<AutomationClient> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        AutomationClient.ConnectAsync(PipeName, timeout, cancellationToken);

    public int ProcessId => process.Id;

    /// <summary>Whether the editor process has exited on its own (a clean shutdown, not a forced kill).</summary>
    public bool HasExited => process.HasExited;

    /// <summary>Completes when the editor process exits, whether it exited on its own or was killed.</summary>
    public Task Exited => exited.Task;

    /// <summary>The editor process's exit code; only valid once <see cref="HasExited"/> is true.</summary>
    public int ExitCode => process.ExitCode;

    public string Output
    {
        get
        {
            lock (output)
            {
                return output.ToString();
            }
        }
    }

    public string Errors
    {
        get
        {
            lock (errors)
            {
                return errors.ToString();
            }
        }
    }

    public static string DesktopAssembly { get; } = typeof(EditorProcess).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "DesktopAssembly").Value ?? throw new InvalidOperationException("The DesktopAssembly metadata has no value.");

    /// <summary>Starts the editor (optionally with a project) and waits until it reports ready.</summary>
    public static Task<EditorProcess> StartAsync(XServer server, string workDirectory, string? project, CancellationToken cancellationToken) =>
        StartAsync(server, workDirectory, new EditorStart(project), cancellationToken);

    /// <summary>Starts the editor as <paramref name="start"/> says and waits until it reports ready.</summary>
    public static async Task<EditorProcess> StartAsync(XServer server, string workDirectory, EditorStart start, CancellationToken cancellationToken)
    {
        string? project = start.Project;
        // Short and outside workDirectory (whose scratchpad-derived path can itself run long): a
        // long-TMPDIR workDirectory pushed this over the 108-character Unix socket limit before.
        string pipe = Path.Combine(Path.GetTempPath(), $"np-e2e-{Guid.NewGuid():N}.sock");
        if (Encoding.UTF8.GetByteCount(pipe) >= UnixSocketPathLimit)
        {
            throw new InvalidOperationException($"Automation pipe path is too long for a Unix socket ({Encoding.UTF8.GetByteCount(pipe)} bytes): {pipe}");
        }

        var info = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { DesktopAssembly },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workDirectory,
        };
        if (project is not null)
        {
            info.ArgumentList.Add(project);
        }

        server.Apply(info.Environment);
        info.Environment[AutomationAgent.EnableVariable] = "1";
        info.Environment[AutomationAgent.PipeVariable] = pipe;
        foreach (var (name, value) in start.Environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start the editor.");
        var editor = default(EditorProcess);
        var output = new StringBuilder();
        var errors = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(errors, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            var client = await ConnectOrFailFastAsync(process, pipe, TimeSpan.FromSeconds(60), errors, cancellationToken);
            editor = new EditorProcess(process, client, pipe, output, errors);

            // Ready: the main window is shown and, with a project, the project and its types are loaded.
            var clock = Stopwatch.StartNew();
            while (true)
            {
                var status = await client.StatusAsync(cancellationToken);
                if (status.MainWindowShown && (project is null || !start.WaitForProject || (status.ProjectLoaded && status.ReflectionLoaded)))
                {
                    break;
                }

                if (clock.Elapsed > TimeSpan.FromSeconds(90))
                {
                    throw new TimeoutException($"The editor did not report ready: {status}");
                }

                await Task.Delay(50, cancellationToken);
            }

            return editor;
        }
        catch
        {
            if (editor is not null)
            {
                await editor.DisposeAsync();
            }
            else if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }

    private EditorProcess(Process process, AutomationClient client, string pipeName, StringBuilder output, StringBuilder errors)
        : this(process, client, pipeName)
    {
        this.output = output;
        this.errors = errors;
    }

    /// <summary>
    /// Connects, but does not wait out the full timeout if the editor process exits first (e.g.
    /// the automation agent failed to bind its pipe and the process crashed on startup): polls for
    /// exit and throws immediately with the exit code and captured stderr, instead of leaving the
    /// caller to a 60-second timeout with no indication of what went wrong.
    /// </summary>
    private static async Task<AutomationClient> ConnectOrFailFastAsync(
        Process process, string pipe, TimeSpan timeout, StringBuilder errors, CancellationToken cancellationToken)
    {
        var connecting = AutomationClient.ConnectAsync(pipe, timeout, cancellationToken);
        while (true)
        {
            if (await Task.WhenAny(connecting, Task.Delay(200, cancellationToken)) == connecting)
            {
                return await connecting;
            }

            if (process.HasExited)
            {
                string capturedErrors;
                lock (errors)
                {
                    capturedErrors = errors.ToString();
                }

                throw new InvalidOperationException(
                    $"The editor process exited (code {process.ExitCode}) before the automation agent came up.\nStderr:\n{capturedErrors}");
            }
        }
    }

    private static void Append(StringBuilder builder, string? line)
    {
        if (line is not null)
        {
            lock (builder)
            {
                builder.AppendLine(line);
            }
        }
    }

    /// <summary>Kills the editor, for the test that proves the diagnostics of an editor that exits.</summary>
    public void Kill() => process.Kill(entireProcessTree: true);

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }
}
