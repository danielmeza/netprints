#nullable enable
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Translator;

/// <summary>
/// The offset, in an execution graph's own unformatted translated body, where one node's statements
/// start (research.md R3): recorded by
/// <see cref="ExecutionGraphTranslator"/> and consumed only by <see cref="ClassTranslator"/> to build a
/// <see cref="SourceMap"/> once the member's text lands in the class's full generated code.
/// </summary>
/// <param name="Offset">Character offset into the member's own unformatted code.</param>
/// <param name="NodeId">Id of the node whose statements start at <paramref name="Offset"/>.</param>
internal readonly record struct NodeOffset(int Offset, string NodeId);

/// <summary>
/// One mapped region of a <see cref="TranslatedClass"/>'s generated code (compilation-and-diagnostics.md
/// §2): every position in <see cref="Span"/> was produced by translating <see cref="NodeId"/> of the
/// graph keyed <see cref="GraphKey"/> (<c>NetPrints.Core.GraphKeys.For</c>).
/// </summary>
/// <param name="Span">Region of the generated code this entry covers.</param>
/// <param name="GraphKey">Graph key of the node's graph.</param>
/// <param name="NodeId">Id of the node.</param>
public readonly record struct SourceMapEntry(TextSpan Span, string GraphKey, string NodeId);

/// <summary>
/// Maps positions in a <see cref="TranslatedClass"/>'s generated code back to the node that produced
/// them (FR-033, research.md R3), without the map changing that code (RC-T06): the class translator
/// annotates the token at each recorded <see cref="NodeOffset"/> with a <see cref="Microsoft.CodeAnalysis.SyntaxAnnotation"/>
/// before formatting, and reads the annotated token spans back from the formatted tree, so the map
/// survives whatever whitespace formatting moves.
/// </summary>
public sealed class SourceMap
{
    private readonly IReadOnlyList<SourceMapEntry> entries;

    /// <summary>
    /// Creates a map from <paramref name="entries"/>, which the caller has already sorted by
    /// <see cref="SourceMapEntry.Span"/>'s start and bounded so consecutive entries of the same member
    /// never overlap.
    /// </summary>
    /// <param name="entries">The map's entries, sorted by <see cref="SourceMapEntry.Span"/> start.</param>
    public SourceMap(IReadOnlyList<SourceMapEntry> entries)
    {
        this.entries = entries;
    }

    /// <summary>A map with no entries, for a class whose members translated no node to a statement.</summary>
    public static SourceMap Empty { get; } = new SourceMap([]);

    /// <summary>Every entry, sorted by <see cref="SourceMapEntry.Span"/> start, non-overlapping.</summary>
    public IReadOnlyList<SourceMapEntry> Entries => entries;

    /// <summary>
    /// Finds the entry whose node produced the code at <paramref name="position"/>.
    /// </summary>
    /// <param name="position">Character offset into the generated code.</param>
    /// <returns>The entry containing <paramref name="position"/>, or <see langword="null"/> if
    /// <paramref name="position"/> is not covered by any member's mapped range (before a member's
    /// first mapped node, or outside every member).</returns>
    public SourceMapEntry? Find(int position)
    {
        int low = 0;
        int high = entries.Count - 1;
        int candidate = -1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (entries[mid].Span.Start <= position)
            {
                candidate = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (candidate < 0)
        {
            return null;
        }

        SourceMapEntry entry = entries[candidate];
        return position < entry.Span.End ? entry : null;
    }
}

/// <summary>
/// A class's generated C# together with the map from its code back to the nodes that produced it
/// (compilation-and-diagnostics.md §2).
/// </summary>
/// <param name="FullName">Full name of the translated class.</param>
/// <param name="Code">The class's generated C#, identical whether or not <paramref name="Map"/> has any
/// entries (RC-T06).</param>
/// <param name="Map">Maps a position in <paramref name="Code"/> back to the node that produced it.</param>
public sealed record TranslatedClass(string FullName, string Code, SourceMap Map);
