using System.Collections.Generic;
using NetPrints.Serialization.Documents;

namespace NetPrints.Cli.Git;

/// <summary>The result of merging three versions of a graph: a merged document, or the conflicts that need a text merge.</summary>
internal abstract record MergeOutcome
{
    private MergeOutcome()
    {
    }

    /// <summary>The merge completed and the merged document is valid.</summary>
    /// <param name="Document">The merged document.</param>
    internal sealed record Clean(ClassDocument Document) : MergeOutcome;

    /// <summary>The merge found conflicts; the caller falls back to a text merge.</summary>
    /// <param name="Conflicts">The conflicts, in the order they were found; never empty.</param>
    internal sealed record Conflicted(IReadOnlyList<MergeConflict> Conflicts) : MergeOutcome;
}
