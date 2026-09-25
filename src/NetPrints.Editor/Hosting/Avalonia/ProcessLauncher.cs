using System.Diagnostics;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes, redirecting their console output to <see cref="OutputReceived"/>
/// (the editor's Output pane) instead of the editor's own terminal.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc/>
    public event Action<string>? OutputReceived;

    /// <inheritdoc/>
    public void Start(string fileName, string? arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments ?? "")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
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
