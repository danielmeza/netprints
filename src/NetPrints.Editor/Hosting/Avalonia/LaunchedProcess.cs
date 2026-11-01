using System.ComponentModel;
using System.Diagnostics;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// The handle of one started process: its input writer, the input and exit flags and the stop rules.
/// It does not own the <see cref="Process"/>; <see cref="ProcessSupervisor"/> does, and reports the exit through <see cref="MarkExited"/>.
/// </summary>
internal sealed class LaunchedProcess : IRunningProcess
{
    private readonly Process process;
    private readonly StreamWriter? input;
    private readonly SemaphoreSlim writeGate;
    private readonly Lock inputLock = new();
    private bool inputClosed;
    private volatile bool exited;

    public LaunchedProcess(int id, Process process, bool redirectInput, SemaphoreSlim writeGate)
    {
        Id = id;
        this.process = process;
        this.writeGate = writeGate;
        input = redirectInput ? process.StandardInput : null;
    }

    /// <inheritdoc/>
    public int Id { get; }

    /// <inheritdoc/>
    public bool HasExited => exited;

    /// <inheritdoc/>
    public bool CanWriteInput
    {
        get
        {
            lock (inputLock)
            {
                return input is not null && !inputClosed && !exited;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsHandedOff => false;

    /// <inheritdoc/>
    public event EventHandler? InputStateChanged;

    /// <inheritdoc/>
    public async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (input is null || !CanWriteInput)
        {
            throw new InvalidOperationException("The process does not accept input.");
        }

        try
        {
            await writeGate.WaitAsync(cancellationToken);
            try
            {
                if (!CanWriteInput)
                {
                    throw new InvalidOperationException("The process does not accept input.");
                }

                await input.WriteLineAsync(line.AsMemory(), cancellationToken);
                await input.FlushAsync(cancellationToken);
            }
            finally
            {
                writeGate.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            CloseInput();
            throw new InvalidOperationException("The process closed its input.", ex);
        }
    }

    /// <inheritdoc/>
    public void CloseInput()
    {
        bool changed;
        lock (inputLock)
        {
            changed = input is not null && !inputClosed && !exited;
            inputClosed = true;
        }

        if (!changed)
        {
            return;
        }

        try
        {
            input?.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // the process already closed its end
        }

        InputStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Kill();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Kill()
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // already exited
        }
        catch (Win32Exception) when (exited)
        {
            // a process that is going away refuses the kill
        }
    }

    /// <summary>Records that the process exited, so input is no longer accepted, and raises <see cref="InputStateChanged"/> when it had been.</summary>
    public void MarkExited()
    {
        bool couldWrite;
        lock (inputLock)
        {
            couldWrite = input is not null && !inputClosed && !exited;
            exited = true;
        }

        if (couldWrite)
        {
            InputStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
