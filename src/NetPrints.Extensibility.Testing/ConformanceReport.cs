using System.Text;

namespace NetPrints.Extensibility.Testing;

/// <summary>The outcome of one conformance check.</summary>
public enum ConformanceStatus
{
    /// <summary>The extension follows the rule.</summary>
    Passed,

    /// <summary>The extension breaks the rule; the findings say how.</summary>
    Failed,

    /// <summary>The check could not run, for example because it needs a folder or a harness factory the options do not give.</summary>
    Skipped,
}

/// <summary>One violation of a rule: what rule, and what item breaks it.</summary>
/// <param name="Code">The <c>NPK</c> code of the check.</param>
/// <param name="Rule">The rule that is broken, in a sentence.</param>
/// <param name="Item">The offending item: a manifest field, a file, a node kind or a type.</param>
public sealed record ConformanceFinding(string Code, string Rule, string Item)
{
    /// <summary>Formats the finding as <c>NPK001 rule: item</c>.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Code} {Rule}: {Item}";
}

/// <summary>The result of one check.</summary>
/// <param name="Code">The <c>NPK</c> code.</param>
/// <param name="Name">The short name of the check.</param>
/// <param name="Status">Whether it passed, failed or was skipped.</param>
/// <param name="Findings">The violations; empty unless <paramref name="Status"/> is <see cref="ConformanceStatus.Failed"/>.</param>
/// <param name="Note">Why the check was skipped, or what it covered; may be empty.</param>
public sealed record ConformanceCheckResult(string Code, string Name, ConformanceStatus Status, IReadOnlyList<ConformanceFinding> Findings, string Note);

/// <summary>The results of a conformance run.</summary>
public sealed class ConformanceReport
{
    /// <summary>Creates a report.</summary>
    /// <param name="results">The check results, in the order the checks ran.</param>
    public ConformanceReport(IReadOnlyList<ConformanceCheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Results = results;
    }

    /// <summary>Gets the check results, in the order the checks ran.</summary>
    public IReadOnlyList<ConformanceCheckResult> Results { get; }

    /// <summary>Gets a value indicating whether no check failed. Skipped checks do not count as failures.</summary>
    public bool Passed => Results.All(result => result.Status != ConformanceStatus.Failed);

    /// <summary>Finds the result of a check.</summary>
    /// <param name="code">The <c>NPK</c> code.</param>
    /// <returns>The result, or <see langword="null"/> when the check did not run.</returns>
    public ConformanceCheckResult? Find(string code) => Results.FirstOrDefault(result => string.Equals(result.Code, code, StringComparison.Ordinal));

    /// <summary>Formats the report, one line per check and one per finding.</summary>
    /// <returns>The text.</returns>
    public string Format()
    {
        var text = new StringBuilder();
        foreach (ConformanceCheckResult result in Results)
        {
            text.Append(result.Code).Append(' ').Append(result.Name).Append(": ").Append(result.Status);
            if (result.Note.Length > 0)
            {
                text.Append(" (").Append(result.Note).Append(')');
            }

            text.AppendLine();
            foreach (ConformanceFinding finding in result.Findings)
            {
                text.Append("  ").Append(finding).AppendLine();
            }
        }

        return text.ToString();
    }
}
