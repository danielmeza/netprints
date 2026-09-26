#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Projects;

/// <summary>
/// <see cref="IProcessRunner"/> backed by <see cref="System.Diagnostics.Process"/> (project-system.md §4).
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public async Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        try
        {
            // Read both streams concurrently, not one after WaitForExitAsync: a process that fills its
            // stderr buffer while only stdout is being drained (or vice versa) would otherwise deadlock.
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(standardOutputTask, standardErrorTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            return new ProcessResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited on its own between the check above and the kill attempt.
        }
    }
}
