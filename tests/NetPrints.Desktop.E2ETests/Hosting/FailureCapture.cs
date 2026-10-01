using System.Collections.Concurrent;
using NetPrints.Editor.Hosting;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>One file of the failure diagnostics and the code that writes it.</summary>
/// <param name="Name">What the part captures; named in <c>capture-errors.txt</c> when it fails.</param>
/// <param name="FileName">The file it writes inside the capture folder.</param>
/// <param name="WriteAsync">Writes the file at the given path; honours the token when it can.</param>
public sealed record CapturePart(string Name, string FileName, Func<string, CancellationToken, Task> WriteAsync);

/// <summary>
/// Captures the diagnostics of a failed E2E scenario (contracts/ci.md §3): runs every
/// <see cref="CapturePart"/> with its own time limit and an overall one, so a hung editor cannot
/// block the capture, and records the parts that failed in <c>capture-errors.txt</c>.
/// </summary>
public sealed class FailureCapture(TimeProvider timeProvider, IReadOnlyList<CapturePart> parts, TimeSpan? partLimit = null)
{
    /// <summary>The file naming the parts that could not be captured.</summary>
    public const string ErrorsFileName = "capture-errors.txt";

    /// <summary>How long one part may take before it is abandoned.</summary>
    public static readonly TimeSpan PartLimit = TimeSpan.FromSeconds(10);

    /// <summary>How long the whole capture may take.</summary>
    public static readonly TimeSpan TotalLimit = TimeSpan.FromSeconds(30);

    private readonly TimeSpan perPart = partLimit ?? PartLimit;

    /// <summary>The folder for the diagnostics of one test class, under <c>TestResults/e2e-diagnostics</c>.</summary>
    public static string FolderFor(string testClass) => Path.Combine(StepTimer.ResultsDirectory, "e2e-diagnostics", testClass);

    /// <summary>Writes every part into <paramref name="directory"/>; parts that fail or hang are recorded, never thrown.</summary>
    /// <param name="directory">The capture folder, created if needed.</param>
    /// <param name="cancellationToken">Abandons the capture.</param>
    public async Task CaptureAsync(string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var errors = new ConcurrentQueue<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Every part starts (and arms its timer) before the first await below.
        var running = parts.ToDictionary(part => part, part => RunPartAsync(part, directory, errors, stop.Token));
        var total = Task.Delay(TotalLimit, timeProvider, stop.Token);
        var finished = Task.WhenAll(running.Values);

        if (await Task.WhenAny(finished, total) != finished)
        {
            foreach (var (part, task) in running.Where(p => !p.Value.IsCompleted))
            {
                errors.Enqueue($"{part.Name}: the capture exceeded {TotalLimit.TotalSeconds:0} s, abandoned");
            }
        }

        if (!errors.IsEmpty)
        {
            await File.WriteAllLinesAsync(Path.Combine(directory, ErrorsFileName), errors, cancellationToken);
        }

        await stop.CancelAsync();
    }

    /// <summary>
    /// Captures into <paramref name="directory"/>, then returns the failure to report: the original,
    /// wrapped with the open step. A capture that itself fails never replaces the original.
    /// </summary>
    /// <param name="original">The failure of the scenario.</param>
    /// <param name="step">The open step's name, or <see langword="null"/>.</param>
    /// <param name="elapsed">How long that step had been running.</param>
    /// <param name="directory">The capture folder.</param>
    /// <param name="cancellationToken">Abandons the capture.</param>
    public async Task<E2EStepFailureException> FailAsync(Exception original, string? step, TimeSpan elapsed, string directory, CancellationToken cancellationToken)
    {
        try
        {
            await CaptureAsync(directory, cancellationToken);
        }
        catch (Exception captureFailure) when (captureFailure is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"[diagnostics] capture into {directory} failed: {captureFailure.Message}");
        }

        return new E2EStepFailureException(step, elapsed, original);
    }

    private async Task RunPartAsync(CapturePart part, string directory, ConcurrentQueue<string> errors, CancellationToken stop)
    {
        using var own = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var limit = Task.Delay(perPart, timeProvider, own.Token);
        Task write;
        try
        {
            write = part.WriteAsync(Path.Combine(directory, part.FileName), own.Token);
        }
        catch (Exception e)
        {
            errors.Enqueue($"{part.Name}: {e.GetType().Name}: {e.Message}");
            await own.CancelAsync();
            return;
        }

        if (await Task.WhenAny(write, limit) == write)
        {
            try
            {
                await write;
            }
            catch (Exception e)
            {
                errors.Enqueue($"{part.Name}: {e.GetType().Name}: {e.Message}");
            }
        }
        else
        {
            if (!stop.IsCancellationRequested)
            {
                errors.Enqueue($"{part.Name}: no result after {perPart.TotalSeconds:0} s, abandoned");
            }

            write.Forget(_ => { });
        }

        await own.CancelAsync();
    }
}
