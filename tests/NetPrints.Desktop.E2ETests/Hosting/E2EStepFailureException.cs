using System.Globalization;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// The failure a scenario reports once its diagnostics are captured: names the step that was
/// running and for how long, and keeps the original failure unchanged as <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class E2EStepFailureException : Exception
{
    /// <summary>Wraps <paramref name="original"/> with the step that was open when it happened.</summary>
    /// <param name="step">The open step's name, or <see langword="null"/> when none was open.</param>
    /// <param name="elapsed">How long that step had been running.</param>
    /// <param name="original">The failure being reported; becomes the inner exception.</param>
    public E2EStepFailureException(string? step, TimeSpan elapsed, Exception original)
        : base($"[step '{step ?? "(none)"}' running for {Math.Round(elapsed.TotalSeconds).ToString("0", CultureInfo.InvariantCulture)} s] {original.Message}", original)
    {
    }
}
