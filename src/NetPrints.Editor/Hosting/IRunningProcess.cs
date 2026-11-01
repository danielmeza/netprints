namespace NetPrints.Editor.Hosting;

/// <summary>A live process's input and termination; returned by <see cref="IProcessLauncher.Start"/>.</summary>
public interface IRunningProcess
{
    /// <summary>The start id carried by <see cref="IProcessLauncher.ProcessStarted"/>, <see cref="IProcessLauncher.LineReceived"/> and <see cref="IProcessLauncher.ProcessExited"/>.</summary>
    int Id { get; }

    /// <summary>Whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Whether input can be written: the request redirected it, it was not closed and the process has not exited.</summary>
    bool CanWriteInput { get; }

    /// <summary>Raised when <see cref="CanWriteInput"/> changed; on any thread.</summary>
    event EventHandler? InputStateChanged;

    /// <summary>Writes <paramref name="line"/> to the process's input as UTF-8 followed by <see cref="Environment.NewLine"/>, and flushes.</summary>
    /// <param name="line">The text of the line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the line was flushed.</returns>
    /// <exception cref="InvalidOperationException"><see cref="CanWriteInput"/> is <see langword="false"/>.</exception>
    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    /// <summary>Ends the process's input (end of file). Idempotent.</summary>
    void CloseInput();

    /// <summary>Asks the process to end, then kills its tree after <paramref name="timeout"/>, or at once when the request could not be sent.</summary>
    /// <param name="timeout">How long the process may take to end by itself.</param>
    /// <param name="cancellationToken">Cancels the wait; the process is then killed.</param>
    /// <returns>A task that completes when the process has exited.</returns>
    Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Kills the process and its child processes now. Idempotent.</summary>
    void Kill();

    /// <summary>Whether this is an external-terminal start NetPrints does not follow (Linux, macOS); <see langword="false"/> otherwise.</summary>
    bool IsHandedOff { get; }
}
