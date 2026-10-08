using System.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes, redirecting their console output to <see cref="OutputReceived"/>
/// (the editor's Output pane) instead of the editor's own terminal.</summary>
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
    public void Start(ProcessStartRequest request, CancellationToken cancellationToken = default)
    {
        int id = Interlocked.Increment(ref lastId);
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is not null)
        {
            foreach ((string key, string value) in request.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        CancellationTokenRegistration killOnCancel = default;

        process.OutputDataReceived += (_, e) => Report(id, ProcessStream.Output, e.Data);
        process.ErrorDataReceived += (_, e) => Report(id, ProcessStream.Error, e.Data);
        process.Exited += (_, _) => _ = ReportExitAsync();

        process.Start();
        killOnCancel = cancellationToken.Register(() => Kill(process)); // before exit events are enabled, so the exit disposes this registration
        ProcessStarted?.Invoke(id, request);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.EnableRaisingEvents = true; // after the start is reported, so a fast exit never precedes it

        async Task ReportExitAsync()
        {
            try
            {
                using var drain = new CancellationTokenSource(DrainTimeout);
                await process.WaitForExitAsync(drain.Token); // every line precedes the exit
            }
            catch (OperationCanceledException)
            {
                // a grandchild holds the pipes open: report the exit anyway
            }

            int code = process.ExitCode;
            ProcessExited?.Invoke(id, code);
            OutputReceived?.Invoke($"Process exited (code {code}).");
            await killOnCancel.DisposeAsync();
            process.Dispose();
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // already exited
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
