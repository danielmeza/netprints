using System.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes, redirecting their console output to <see cref="OutputReceived"/>
/// (the editor's Output pane) instead of the editor's own terminal.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc/>
    public event Action<string>? OutputReceived;

    /// <inheritdoc/>
    public void Start(ProcessStartRequest request)
    {
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

        process.OutputDataReceived += (_, e) => Report(e.Data);
        process.ErrorDataReceived += (_, e) => Report(e.Data);
        process.Exited += (_, _) =>
        {
            OutputReceived?.Invoke($"Process exited (code {process.ExitCode}).");
            process.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void Report(string? line)
    {
        if (line is not null)
        {
            OutputReceived?.Invoke(line);
        }
    }
}
