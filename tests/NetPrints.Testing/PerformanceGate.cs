using System.Diagnostics;
using System.Globalization;

namespace NetPrints.Testing
{
    /// <summary>
    /// The in-process half of the SC-005 time gate (M1, M2). The gate is a same-machine A/B comparison with R5-0:
    /// <c>eng/perf/ab.sh</c> runs a measured test of the head and of R5-0 in alternation, each run in a fresh process with
    /// <see cref="OutputVariable"/> naming a file. The test then measures its steady state with <see cref="MeasureAsync"/>
    /// and appends it with <see cref="Record"/>; the script compares the two trees. Without the variable the tests only
    /// print their first-use numbers. Both trees run this same file (the script copies it into R5-0).
    /// </summary>
    public static class PerformanceGate
    {
        /// <summary>The variable that names the file a measured test appends its steady state to; set by <c>eng/perf/ab.sh</c>.</summary>
        public const string OutputVariable = "NETPRINTS_PERF_AB_OUTPUT";

        private static readonly TimeSpan WarmUpMinimum = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan WarmUpMaximum = TimeSpan.FromSeconds(6);
        private const int WarmUpAgreeing = 3;
        private const double WarmUpAgreement = 1.10;

        /// <summary>The file named by <see cref="OutputVariable"/>, or <see langword="null"/> when the run is not an A/B run.</summary>
        public static string? OutputPath => Environment.GetEnvironmentVariable(OutputVariable) is { Length: > 0 } path ? path : null;

        /// <summary>The median of the values (the upper middle one for an even count).</summary>
        /// <param name="values">At least one value.</param>
        /// <returns>The median.</returns>
        public static double Median(IReadOnlyList<double> values)
        {
            double[] sorted = [.. values.Order()];
            return sorted[sorted.Length / 2];
        }

        /// <summary>
        /// Starts one sample: a full blocking collection first, so every sample starts from the same heap, then the clock.
        /// Call it after the sample's setup and stop it right after the measured work.
        /// </summary>
        /// <returns>The running clock.</returns>
        public static SampleClock StartSample()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return new SampleClock();
        }

        /// <summary>
        /// Runs <paramref name="sampleOnce"/> (one sample, timed with <see cref="StartSample"/>) for at least a second and
        /// until three samples in a row agree within 10% (at most six seconds), then measures <paramref name="samples"/> more.
        /// </summary>
        /// <param name="sampleOnce">One sample.</param>
        /// <param name="samples">The number of samples after the warm-up, at least one.</param>
        /// <returns>The fastest and the median time of the samples, and their median allocation.</returns>
        public static async ValueTask<SteadyState> MeasureAsync(Func<ValueTask<Sample>> sampleOnce, int samples)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);
            var recent = new Queue<double>();
            var elapsed = Stopwatch.StartNew();
            int warmUpRuns = 0;
            while (elapsed.Elapsed < WarmUpMaximum)
            {
                warmUpRuns++;
                recent.Enqueue((await sampleOnce()).Milliseconds);
                if (recent.Count > WarmUpAgreeing)
                {
                    recent.Dequeue();
                }

                if (elapsed.Elapsed >= WarmUpMinimum && recent.Count == WarmUpAgreeing && recent.Max() <= recent.Min() * WarmUpAgreement)
                {
                    break;
                }
            }

            TimeSpan warmUp = elapsed.Elapsed;
            var measured = new List<Sample>(samples);
            for (int sample = 0; sample < samples; sample++)
            {
                measured.Add(await sampleOnce());
            }

            double[] times = [.. measured.Select(sample => sample.Milliseconds)];
            return new SteadyState(
                times.Min(),
                Median(times),
                (long)Median([.. measured.Select(sample => (double)sample.Bytes)]),
                Median([.. measured.Select(sample => sample.GcPauseMilliseconds)]),
                measured.Count,
                warmUpRuns,
                warmUp.TotalSeconds,
                elapsed.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// Appends <paramref name="state"/> to <paramref name="path"/> as one line that <c>eng/perf/ab.sh</c> reads: the metric,
        /// the fastest and the median time, the median bytes, the median GC pause, the count, the warm-up runs, the warm-up
        /// seconds and the total seconds.
        /// </summary>
        /// <param name="path">The file named by <see cref="OutputVariable"/>.</param>
        /// <param name="metric">The metric id, <c>M1</c> or <c>M2</c>; the script reads the lines that start with it.</param>
        /// <param name="state">The measurement.</param>
        public static void Record(string path, string metric, SteadyState state) =>
            File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture,
                $"{metric} {state.Fastest:F3} {state.Median:F3} {state.Bytes} {state.GcPause:F3} {state.Count} {state.WarmUpRuns} {state.WarmUpSeconds:F2} {state.TotalSeconds:F2}{Environment.NewLine}"));
    }

    /// <summary>The clock of one sample, from <see cref="PerformanceGate.StartSample"/>.</summary>
    public sealed class SampleClock
    {
        private readonly long allocated = GC.GetTotalAllocatedBytes(precise: true);
        private readonly TimeSpan paused = GC.GetTotalPauseDuration();
        private readonly long started = Stopwatch.GetTimestamp();

        /// <summary>
        /// Stops the clock. The time leaves out the GC pauses inside the sample: whether a collection falls inside one depends
        /// on the machine's gen0 budget, so the pauses would make two machines disagree; the bytes carry the collector's cost.
        /// </summary>
        /// <returns>The sample.</returns>
        public Sample Stop()
        {
            long ended = Stopwatch.GetTimestamp();
            TimeSpan pause = GC.GetTotalPauseDuration() - paused;
            long bytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            return new Sample((Stopwatch.GetElapsedTime(started, ended) - pause).TotalMilliseconds, bytes, pause.TotalMilliseconds);
        }
    }

    /// <summary>One sample of <see cref="PerformanceGate.MeasureAsync"/>.</summary>
    /// <param name="Milliseconds">The time without the GC pauses inside the sample.</param>
    /// <param name="Bytes">The bytes the process allocated during the sample.</param>
    /// <param name="GcPauseMilliseconds">The GC pauses inside the sample, left out of <paramref name="Milliseconds"/>.</param>
    public readonly record struct Sample(double Milliseconds, long Bytes, double GcPauseMilliseconds);

    /// <summary>A steady-state measurement of <see cref="PerformanceGate.MeasureAsync"/>.</summary>
    /// <param name="Fastest">The fastest sample, in milliseconds.</param>
    /// <param name="Median">The median sample, in milliseconds.</param>
    /// <param name="Bytes">The median bytes allocated by a sample.</param>
    /// <param name="GcPause">The median GC pause inside a sample, in milliseconds (left out of the times).</param>
    /// <param name="Count">The number of samples.</param>
    /// <param name="WarmUpRuns">The samples before the measured ones.</param>
    /// <param name="WarmUpSeconds">The time of the warm-up.</param>
    /// <param name="TotalSeconds">The time of the warm-up and the samples.</param>
    public sealed record SteadyState(double Fastest, double Median, long Bytes, double GcPause, int Count, int WarmUpRuns, double WarmUpSeconds, double TotalSeconds)
    {
        /// <summary>The numbers, for the test output.</summary>
        /// <param name="metric">The metric id.</param>
        /// <returns>One line.</returns>
        public string Describe(string metric) => string.Create(CultureInfo.InvariantCulture,
            $"{metric} steady state: fastest {Fastest:F1} ms, median {Median:F1} ms of {Count} runs (GC pauses left out, median {GcPause:F1} ms), {Bytes} bytes; after {WarmUpRuns} warm-up runs in {WarmUpSeconds:F1} s, {TotalSeconds:F1} s in all.");
    }
}
