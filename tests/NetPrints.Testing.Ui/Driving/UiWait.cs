using System.Diagnostics;

namespace NetPrints.Testing.Ui.Driving;

/// <summary>
/// Condition-based waiting: never sleep for a fixed time. Polls a condition, letting the UI settle
/// between polls, until it holds or the timeout or the test's token ends the wait.
/// </summary>
public static class UiWait
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    public static async Task UntilAsync(IUiDriver driver, Func<Task<bool>> condition, string what,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var clock = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await driver.SettleAsync(cancellationToken);

            if (await condition())
            {
                return;
            }

            if (clock.Elapsed > limit)
            {
                throw new UiWaitTimeoutException(what, limit, await driver.DumpAsync(cancellationToken));
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    /// <summary>Waits for a value that satisfies a condition and returns it.</summary>
    public static async Task<T> ForAsync<T>(IUiDriver driver, Func<Task<T>> read, Func<T, bool> accept, string what,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        T value = default!;
        await UntilAsync(driver, async () => accept(value = await read()), what, cancellationToken, timeout);
        return value;
    }
}

/// <summary>A wait that ran out of time; carries a UI dump for the failure diagnostics.</summary>
public sealed class UiWaitTimeoutException(string what, TimeSpan timeout, string dump)
    : TimeoutException($"Timed out after {timeout.TotalSeconds:0.#} s waiting for: {what}")
{
    public string Dump { get; } = dump;
}
