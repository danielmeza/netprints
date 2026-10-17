using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NetPrints.Testing
{
    /// <summary>
    /// The SC-005 time gate (M1, M2): a measurement is compared with its R5-0 baseline at 10%. A shared runner's speed
    /// varies by tens of percent between runs and a process spends its first runs in the JIT's lower tiers, so the
    /// gate compares a steady-state median, divided by a fixed workload measured in the same process (the calibration),
    /// with the same ratio at the baseline commit. The gate is judged only when <see cref="Strict"/> is set (the
    /// non-blocking perf job); every other run records the numbers.
    /// </summary>
    public static class PerformanceGate
    {
        /// <summary>The allowed growth over the baseline: 10%.</summary>
        public const double Tolerance = 1.10;

        /// <summary>Set to <c>1</c> by the perf job.</summary>
        public const string StrictVariable = "NETPRINTS_PERF_STRICT";

        private static readonly TimeSpan WarmUpMinimum = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan WarmUpMaximum = TimeSpan.FromSeconds(6);
        private const int WarmUpAgreeing = 3;
        private const double WarmUpAgreement = 1.10;
        private const int Samples = 9;
        private const int CalibrationSamples = 15;
        private const int Attempts = 3;

        /// <summary>Whether the gate is judged in this run.</summary>
        public static bool Strict => Environment.GetEnvironmentVariable(StrictVariable) == "1";

        /// <summary>The median of the values.</summary>
        public static double Median(IReadOnlyList<double> values)
        {
            double[] sorted = [.. values.Order()];
            return sorted[sorted.Length / 2];
        }

        /// <summary>The time of the fixed calibration workload on this machine, in milliseconds, once warm.</summary>
        public static async ValueTask<double> CalibrateAsync()
        {
            await WarmUpAsync(() => new ValueTask<double>(Task.Run(CalibrationRun)));
            double fastest = double.PositiveInfinity;
            for (int run = 0; run < CalibrationSamples; run++)
            {
                fastest = Math.Min(fastest, await Task.Run(CalibrationRun));
            }

            return fastest;
        }

        /// <summary>
        /// Runs <paramref name="timeOnce"/> (one measured run, in milliseconds) until the last runs agree, then returns
        /// the median of nine more. When that median is over <paramref name="limitMilliseconds"/> the nine are measured
        /// again, up to three times, and the lowest median is returned: noise only adds time, a regression stays.
        /// </summary>
        public static async ValueTask<double> SteadyMedianAsync(Func<ValueTask<double>> timeOnce, double limitMilliseconds)
        {
            await WarmUpAsync(timeOnce);
            double best = double.PositiveInfinity;
            for (int attempt = 1; attempt <= Attempts && (attempt == 1 || best > limitMilliseconds); attempt++)
            {
                var samples = new List<double>(Samples);
                for (int sample = 0; sample < Samples; sample++)
                {
                    samples.Add(await timeOnce());
                }

                best = Math.Min(best, Median(samples));
            }

            return best;
        }

        /// <summary>Compares a measurement with the baseline ratio at <see cref="Tolerance"/>.</summary>
        public static GateVerdict Judge(string metric, double measuredMilliseconds, double calibrationMilliseconds, double baselineRatio)
        {
            double ratio = measuredMilliseconds / calibrationMilliseconds;
            double limit = baselineRatio * Tolerance;
            bool passed = ratio <= limit;
            string message = string.Create(CultureInfo.InvariantCulture,
                $"{metric}: {measuredMilliseconds:F1} ms against a calibration of {calibrationMilliseconds:F1} ms is a ratio of {ratio:F3}; the baseline is {baselineRatio:F3}, the gate {limit:F3} (+10%), {ratio / baselineRatio - 1:+0.0%;-0.0%} over the baseline.");
            return new GateVerdict(passed, message);
        }

        private static async ValueTask WarmUpAsync(Func<ValueTask<double>> timeOnce)
        {
            var recent = new Queue<double>();
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < WarmUpMaximum)
            {
                recent.Enqueue(await timeOnce());
                if (recent.Count > WarmUpAgreeing)
                {
                    recent.Dequeue();
                }

                if (elapsed.Elapsed >= WarmUpMinimum && recent.Count == WarmUpAgreeing && recent.Max() <= recent.Min() * WarmUpAgreement)
                {
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static double CalibrationRun()
        {
            var stopwatch = Stopwatch.StartNew();
            int sum = 0;
            for (int outer = 0; outer < 6000; outer++)
            {
                var keys = new List<string>(64);
                var map = new Dictionary<string, int>(64);
                for (int inner = 0; inner < 64; inner++)
                {
                    string key = string.Create(CultureInfo.InvariantCulture, $"k{outer}-{inner}");
                    keys.Add(key);
                    map[key] = inner;
                }

                foreach (string key in keys)
                {
                    sum += map[key];
                }
            }

            stopwatch.Stop();
            GC.KeepAlive(sum);
            return stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    /// <summary>The outcome of <see cref="PerformanceGate.Judge"/>.</summary>
    /// <param name="Passed">Whether the measurement is within the gate.</param>
    /// <param name="Message">The numbers, for the test output and the failure.</param>
    public sealed record GateVerdict(bool Passed, string Message);
}
