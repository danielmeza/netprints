using System.Diagnostics;
using System.Text;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes, redirecting their console output to <see cref="OutputReceived"/>
/// (the editor's Output pane) instead of the editor's own terminal.</summary>
[Singleton<IProcessLauncher>]
public sealed class ProcessLauncher : IProcessLauncher
{
    private int lastId;

    /// <summary>How long an exit waits for the redirected streams to drain before it is reported anyway.</summary>
    internal TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public event Action<string>? OutputReceived;

    /// <inheritdoc/>
    public event Action<int, ProcessStartRequest>? ProcessStarted;

    /// <inheritdoc/>
    public event Action<int, ProcessStream, string>? LineReceived;

    /// <inheritdoc/>
    public event Action<int, int>? ProcessExited;

    /// <inheritdoc/>
    public IRunningProcess Start(ProcessStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int id = Interlocked.Increment(ref lastId);
        bool redirectInput = request.CaptureOutput && request.RedirectInput;
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = request.CaptureOutput,
            RedirectStandardError = request.CaptureOutput,
            RedirectStandardInput = redirectInput,
        };

        if (redirectInput)
        {
            startInfo.StandardInputEncoding = new UTF8Encoding(false);
        }

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (string name in request.EnvironmentRemovals)
        {
            if (!string.IsNullOrEmpty(name))
            {
                startInfo.Environment.Remove(name);
            }
        }

        if (request.EnvironmentVariables is not null)
        {
            foreach ((string key, string value) in request.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        var supervisor = new ProcessSupervisor(startInfo, (stream, line) => Report(id, stream, line));
        try
        {
            LaunchedProcess handle = supervisor.Start(id, cancellationToken);
            ProcessStarted?.Invoke(id, request);
            if (request.CaptureOutput)
            {
                supervisor.BeginReads();
            }

            supervisor.Exited += (_, _) => _ = ReportExitAsync(handle);
            supervisor.EnableExitEvents();
            return handle;
        }
        catch
        {
            supervisor.Handle?.Kill();
            supervisor.Dispose();
            throw;
        }

        async Task ReportExitAsync(LaunchedProcess handle)
        {
            if (request.CaptureOutput)
            {
                await supervisor.WaitForDrainAsync(DrainTimeout);
            }

            int code = supervisor.ExitCode;
            handle.MarkExited();
            ProcessExited?.Invoke(id, code);
            OutputReceived?.Invoke($"Process exited (code {code}).");
            supervisor.Dispose();
        }
    }

    private void Report(int id, ProcessStream stream, string? line)
    {
        if (line is not null)
        {
            LineReceived?.Invoke(id, stream, line);
            OutputReceived?.Invoke(line);
        }
    }
}
