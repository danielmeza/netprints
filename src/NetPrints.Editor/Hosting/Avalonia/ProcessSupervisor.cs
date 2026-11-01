using System.Diagnostics;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Owns one started <see cref="Process"/>, its write gate and its kill-on-cancel registration, and hands out the <see cref="LaunchedProcess"/> over them.</summary>
internal sealed class ProcessSupervisor : IDisposable
{
    private readonly Process process;
    private readonly SemaphoreSlim writeGate = new(1);
    private CancellationTokenRegistration killOnCancel;

    public ProcessSupervisor(ProcessStartInfo startInfo, Action<ProcessStream, string?> onLine)
    {
        process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => onLine(ProcessStream.Output, e.Data);
        process.ErrorDataReceived += (_, e) => onLine(ProcessStream.Error, e.Data);
    }

    /// <summary>Raised once the process exited, after <see cref="EnableExitEvents"/>; on a thread-pool thread.</summary>
    public event EventHandler? Exited;

    /// <summary>The handle of the started process; set by <see cref="Start"/>.</summary>
    public LaunchedProcess? Handle { get; private set; }

    /// <summary>The exit code; valid once the process has exited.</summary>
    public int ExitCode => process.ExitCode;

    /// <summary>Starts the process and arranges for it to be killed when <paramref name="cancellationToken"/> is cancelled (at once when it already is).</summary>
    /// <param name="id">The start id.</param>
    /// <param name="cancellationToken">Cancelling it kills the process tree.</param>
    /// <returns>The handle of the started process.</returns>
    public LaunchedProcess Start(int id, CancellationToken cancellationToken)
    {
        process.Start();
        var handle = new LaunchedProcess(id, process, process.StartInfo.RedirectStandardInput, writeGate);
        Handle = handle;
        killOnCancel = cancellationToken.Register(handle.Kill);
        return handle;
    }

    /// <summary>Begins the asynchronous reads of the redirected output and error streams.</summary>
    public void BeginReads()
    {
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    /// <summary>Turns on <see cref="Exited"/>; call it after the start was reported, so that a fast exit never precedes it.</summary>
    public void EnableExitEvents()
    {
        process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);
        process.EnableRaisingEvents = true;
    }

    /// <summary>Waits for the redirected streams to drain, up to <paramref name="drainTimeout"/>, so that every line precedes the exit.</summary>
    /// <param name="drainTimeout">How long to wait before the exit is reported anyway.</param>
    /// <returns>A task that completes when the streams drained or the timeout passed.</returns>
    public async Task WaitForDrainAsync(TimeSpan drainTimeout)
    {
        try
        {
            using var drain = new CancellationTokenSource(drainTimeout);
            await process.WaitForExitAsync(drain.Token);
        }
        catch (OperationCanceledException)
        {
            // a grandchild holds the pipes open: report the exit anyway
        }
    }

    /// <summary>Releases the kill registration, the write gate and the process.</summary>
    public void Dispose()
    {
        killOnCancel.Dispose();
        writeGate.Dispose();
        process.Dispose();
    }
}
