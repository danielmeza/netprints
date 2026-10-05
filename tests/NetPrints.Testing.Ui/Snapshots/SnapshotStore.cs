using System.Diagnostics;

namespace NetPrints.Testing.Ui.Snapshots;

/// <summary>
/// Compares screenshots with committed baseline PNGs. Every actual image is written to the output
/// folder (a CI artifact), with a diff image when it does not match. With
/// <c>NETPRINTS_UPDATE_SNAPSHOTS=1</c> the actual images become the new baselines.
/// </summary>
public sealed class SnapshotStore(string baselineDirectory, string outputDirectory)
{
    public const string UpdateVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    public string BaselineDirectory { get; } = baselineDirectory;

    public string OutputDirectory { get; } = outputDirectory;

    /// <summary>Name of the file in <see cref="OutputDirectory"/> that records the frames each stable capture needed.</summary>
    public const string StabilityLogFile = "stability.txt";

    public static bool IsUpdating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>Checks an image against the baseline <paramref name="name"/>.png.</summary>
    public void Match(string name, UiImage actual, SnapshotOptions? options = null)
    {
        string baselinePath = Path.Combine(BaselineDirectory, name + ".png");
        actual.Save(Path.Combine(OutputDirectory, name + ".actual.png"));

        if (IsUpdating)
        {
            actual.Save(baselinePath);
            return;
        }

        if (!File.Exists(baselinePath))
        {
            throw new SnapshotMismatchException(
                $"No baseline {baselinePath}. The actual image is in {OutputDirectory}; run with {UpdateVariable}=1 to create it.");
        }

        var comparison = SnapshotComparer.Compare(actual, new UiImage(File.ReadAllBytes(baselinePath)), options ?? SnapshotOptions.Default);
        if (!comparison.Matches)
        {
            comparison.Diff?.Save(Path.Combine(OutputDirectory, name + ".diff.png"));
            throw new SnapshotMismatchException($"Snapshot '{name}' does not match its baseline: {comparison.Reason}. See {OutputDirectory}.");
        }
    }

    /// <summary>
    /// Captures frames from <paramref name="capture"/> until two consecutive frames are identical, then checks
    /// that settled frame against the baseline like <see cref="Match"/>. Guards against capturing while
    /// something asynchronous (syntax highlighting, layout, a debounced analysis) is still repainting. The
    /// frames needed are returned and appended to <see cref="StabilityLogFile"/> in the output folder.
    /// </summary>
    /// <param name="name">Baseline name, without extension.</param>
    /// <param name="capture">Takes one frame; called repeatedly.</param>
    /// <param name="options">Tolerance of the comparison with the baseline.</param>
    /// <param name="stable">How long to wait between frames and how many frames or seconds to try; defaults to <see cref="StableCaptureOptions"/>'s.</param>
    /// <param name="cancellationToken">Cancels the capture loop.</param>
    /// <returns>The number of frames taken, the settled one included.</returns>
    /// <exception cref="SnapshotMismatchException">The frames never settled within the budget, or the settled
    /// frame does not match its baseline.</exception>
    public async Task<StableMatch> MatchStableAsync(string name, Func<CancellationToken, Task<UiImage>> capture, SnapshotOptions? options = null,
        StableCaptureOptions? stable = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        stable ??= new StableCaptureOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(stable.MaxFrames, 2);

        var identical = new SnapshotOptions { PixelThreshold = 0, MaxDiffPercent = 0, Masks = options?.Masks ?? [] };
        long started = Stopwatch.GetTimestamp();
        var previous = await capture(cancellationToken);
        SnapshotComparison? last = null;
        int frames = 1;
        while (frames < stable.MaxFrames && Stopwatch.GetElapsedTime(started) < stable.Budget)
        {
            frames++;
            if (stable.Interval > TimeSpan.Zero)
            {
                await Task.Delay(stable.Interval, cancellationToken);
            }

            var current = await capture(cancellationToken);
            last = SnapshotComparer.Compare(current, previous, identical);
            if (last.Matches)
            {
                Directory.CreateDirectory(OutputDirectory);
                File.AppendAllText(Path.Combine(OutputDirectory, StabilityLogFile), $"{name}: {frames} frames{Environment.NewLine}");
                Match(name, current, options);
                return new StableMatch(frames);
            }

            previous = current;
        }

        previous.Save(Path.Combine(OutputDirectory, name + ".actual.png"));
        last?.Diff?.Save(Path.Combine(OutputDirectory, name + ".diff.png"));
        throw new SnapshotMismatchException(
            $"Snapshot '{name}' did not settle after {frames} frames ({stable.Interval.TotalMilliseconds:0} ms apart, budget {stable.Budget.TotalSeconds:0.#} s); the last two frames differ: {last?.Reason}. See {OutputDirectory}.");
    }
}

/// <summary>How <see cref="SnapshotStore.MatchStableAsync"/> waits for a frame to settle.</summary>
public sealed record StableCaptureOptions
{
    /// <summary>Pause between two frames, so asynchronous work gets time to change the next one.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Most frames to take, the first one included, before giving up (at least 2).</summary>
    public int MaxFrames { get; init; } = 100;

    /// <summary>Longest to keep taking frames; it keeps a never-settling capture well below a test's own timeout.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromSeconds(15);
}

/// <summary>The outcome of a stable capture.</summary>
/// <param name="Frames">Frames taken until two consecutive ones were identical, the last one included.</param>
public sealed record StableMatch(int Frames);

public sealed class SnapshotMismatchException(string message) : Exception(message);
