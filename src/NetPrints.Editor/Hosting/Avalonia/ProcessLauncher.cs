using System.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes, redirecting their console output to <see cref="OutputReceived"/>
/// (the editor's Output pane) instead of the editor's own terminal.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    private int lastId;

    /// <inheritdoc/>
    public event Action<string>? OutputReceived;

    /// <inheritdoc/>
    public event Action<int, ProcessStartRequest>? ProcessStarted;

    /// <inheritdoc/>
    public event Action<int, ProcessStream, string>? LineReceived;

    /// <inheritdoc/>
    public event Action<int, int>? ProcessExited;

    /// <inheritdoc/>
    public void Start(ProcessStartRequest request)
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

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, e) => Report(id, ProcessStream.Output, e.Data);
        process.ErrorDataReceived += (_, e) => Report(id, ProcessStream.Error, e.Data);
        process.Exited += (_, _) =>
        {
            process.WaitForExit(); // drains the redirected streams, so every line precedes the exit
            int code = process.ExitCode;
            ProcessExited?.Invoke(id, code);
            OutputReceived?.Invoke($"Process exited (code {code}).");
            process.Dispose();
        };

        process.Start();
        ProcessStarted?.Invoke(id, request);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
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
