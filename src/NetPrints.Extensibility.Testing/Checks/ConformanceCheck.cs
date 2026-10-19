namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>One conformance check: a rule an extension must follow and the code that finds the items that break it.</summary>
internal abstract class ConformanceCheck
{
    /// <summary>Gets the <c>NPK</c> code.</summary>
    public abstract string Code { get; }

    /// <summary>Gets the short name shown in reports.</summary>
    public abstract string Name { get; }

    /// <summary>Runs the check.</summary>
    /// <param name="context">The harness and options.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>The outcome.</returns>
    public abstract Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken);

    /// <summary>Creates a finding of this check.</summary>
    /// <param name="rule">The rule that is broken.</param>
    /// <param name="item">The offending item.</param>
    /// <returns>The finding.</returns>
    protected ConformanceFinding Finding(string rule, string item) => new(Code, rule, item);
}

/// <summary>What a check found.</summary>
/// <param name="Findings">The violations; empty when the extension follows the rule.</param>
/// <param name="SkipReason">Why the check could not run, or <see langword="null"/> when it ran.</param>
/// <param name="Note">A note for the report on a check that ran; may be empty.</param>
internal sealed record CheckOutcome(IReadOnlyList<ConformanceFinding> Findings, string? SkipReason, string Note)
{
    /// <summary>The check ran and found nothing.</summary>
    public static CheckOutcome Pass { get; } = new([], null, string.Empty);

    /// <summary>Creates the outcome of a check that ran and found nothing.</summary>
    /// <param name="note">What the check covered.</param>
    /// <returns>The outcome.</returns>
    public static CheckOutcome Passed(string note) => new([], null, note);

    /// <summary>Creates the outcome of a check that could not run.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The outcome.</returns>
    public static CheckOutcome Skip(string reason) => new([], reason, string.Empty);

    /// <summary>Creates the outcome of a check from its findings.</summary>
    /// <param name="findings">The violations.</param>
    /// <returns>The outcome.</returns>
    public static CheckOutcome FromFindings(IReadOnlyList<ConformanceFinding> findings) => new(findings, null, string.Empty);
}
