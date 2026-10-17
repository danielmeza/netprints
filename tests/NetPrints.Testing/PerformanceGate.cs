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
        /// Runs <paramref name="timeOnce"/> (one measured run, in milliseconds) for at least a second and until three runs
        /// in a row agree within 10% (at most six seconds), then measures <paramref name="samples"/> more.
        /// </summary>
        /// <param name="timeOnce">One measured run; returns its time in milliseconds.</param>
        /// <param name="samples">The number of measured runs after the warm-up, at least one.</param>
        /// <returns>The fastest and the median of the samples.</returns>
        public static async ValueTask<SteadyState> MeasureAsync(Func<ValueTask<double>> timeOnce, int samples)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);
            var recent = new Queue<double>();
            var elapsed = Stopwatch.StartNew();
            int warmUpRuns = 0;
            while (elapsed.Elapsed < WarmUpMaximum)
            {
                warmUpRuns++;
                recent.Enqueue(await timeOnce());
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
            var measured = new List<double>(samples);
            for (int sample = 0; sample < samples; sample++)
            {
                measured.Add(await timeOnce());
            }

            return new SteadyState(measured.Min(), Median(measured), measured.Count, warmUpRuns, warmUp.TotalSeconds, elapsed.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// Appends <paramref name="state"/> to <paramref name="path"/> as one line: the metric, the fastest, the median, the count,
        /// the warm-up runs, the warm-up seconds and the total seconds.
        /// </summary>
        /// <param name="path">The file named by <see cref="OutputVariable"/>.</param>
        /// <param name="metric">The metric id, <c>M1</c> or <c>M2</c>; the script reads the lines that start with it.</param>
        /// <param name="state">The measurement.</param>
        public static void Record(string path, string metric, SteadyState state) =>
            File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture,
                $"{metric} {state.Fastest:F3} {state.Median:F3} {state.Count} {state.WarmUpRuns} {state.WarmUpSeconds:F2} {state.TotalSeconds:F2}{Environment.NewLine}"));
    }

    /// <summary>A steady-state measurement of <see cref="PerformanceGate.MeasureAsync"/>.</summary>
    /// <param name="Fastest">The fastest sample, in milliseconds.</param>
    /// <param name="Median">The median sample, in milliseconds.</param>
    /// <param name="Count">The number of samples.</param>
    /// <param name="WarmUpRuns">The runs before the samples.</param>
    /// <param name="WarmUpSeconds">The time of the warm-up.</param>
    /// <param name="TotalSeconds">The time of the warm-up and the samples.</param>
    public sealed record SteadyState(double Fastest, double Median, int Count, int WarmUpRuns, double WarmUpSeconds, double TotalSeconds)
    {
        /// <summary>The numbers, for the test output.</summary>
        /// <param name="metric">The metric id.</param>
        /// <returns>One line.</returns>
        public string Describe(string metric) => string.Create(CultureInfo.InvariantCulture,
            $"{metric} steady state: fastest {Fastest:F1} ms, median {Median:F1} ms of {Count} runs, after {WarmUpRuns} warm-up runs in {WarmUpSeconds:F1} s; {TotalSeconds:F1} s in all.");
    }
}
