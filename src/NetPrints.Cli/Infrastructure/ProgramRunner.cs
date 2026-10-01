using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Cli.Infrastructure;

/// <summary>
/// <see cref="IProgramRunner"/> over <see cref="Process"/>. By default the child inherits stdin, stdout and stderr, so prompts, ordering,
/// colours and Ctrl+C behave as if the program were started directly; a stream given for an output redirects that output into it instead.
/// </summary>
/// <param name="standardOutput">Receives the child's stdout when not <see langword="null"/>.</param>
/// <param name="standardError">Receives the child's stderr when not <see langword="null"/>.</param>
internal sealed class ProgramRunner(Stream? standardOutput = null, Stream? standardError = null) : IProgramRunner
{
    /// <inheritdoc/>
    public async Task<int> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = standardOutput is not null,
            RedirectStandardError = standardError is not null,
            UseShellExecute = false,
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

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        try
        {
            Task copyOutput = standardOutput is null ? Task.CompletedTask : process.StandardOutput.BaseStream.CopyToAsync(standardOutput, cancellationToken);
            Task copyError = standardError is null ? Task.CompletedTask : process.StandardError.BaseStream.CopyToAsync(standardError, cancellationToken);
            await Task.WhenAll(copyOutput, copyError, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
            return process.ExitCode;
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
