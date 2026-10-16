using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace NetPrints.Authoring.Pins;

/// <summary>The pin keys recorded in <c>NodePins.Shipped.txt</c> and <c>NodePins.Unshipped.txt</c>: one <c>&lt;kind&gt; &lt;direction&gt;.&lt;exec|data|type&gt;.&lt;key&gt;</c> line per pin.</summary>
internal sealed class PinKeyTracking
{
    /// <summary>The file name of the shipped keys.</summary>
    public const string ShippedFile = "NodePins.Shipped.txt";

    /// <summary>The file name of the keys not shipped yet.</summary>
    public const string UnshippedFile = "NodePins.Unshipped.txt";

    /// <summary>The prefix of a line that removes a shipped key on purpose.</summary>
    public const string RemovedPrefix = "*REMOVED*";

    private const char CommentMarker = ';';

    private readonly HashSet<string> shipped;

    private readonly HashSet<string> unshipped;

    private readonly HashSet<string> removed;

    private PinKeyTracking(HashSet<string> shipped, HashSet<string> unshipped, HashSet<string> removed)
    {
        this.shipped = shipped;
        this.unshipped = unshipped;
        this.removed = removed;
    }

    /// <summary>Reads the tracking files among the additional files; null when the project has none, so nothing is tracked.</summary>
    /// <param name="files">The additional files of the compilation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The recorded keys, or null.</returns>
    public static PinKeyTracking? Read(ImmutableArray<AdditionalText> files, CancellationToken token)
    {
        HashSet<string> shipped = new(StringComparer.Ordinal);
        HashSet<string> unshipped = new(StringComparer.Ordinal);
        HashSet<string> removed = new(StringComparer.Ordinal);
        bool found = false;
        foreach (AdditionalText file in files)
        {
            string name = Path.GetFileName(file.Path);
            bool isShipped = string.Equals(name, ShippedFile, StringComparison.Ordinal);
            if (!isShipped && !string.Equals(name, UnshippedFile, StringComparison.Ordinal))
            {
                continue;
            }

            found = true;
            foreach (string raw in (file.GetText(token)?.ToString() ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == CommentMarker)
                {
                    continue;
                }

                if (line.StartsWith(RemovedPrefix, StringComparison.Ordinal))
                {
                    removed.Add(line.Substring(RemovedPrefix.Length));
                }
                else
                {
                    (isShipped ? shipped : unshipped).Add(line);
                }
            }
        }

        return found ? new PinKeyTracking(shipped, unshipped, removed) : null;
    }

    /// <summary>Whether a line is recorded in either file.</summary>
    /// <param name="line">The line.</param>
    /// <returns>True when recorded.</returns>
    public bool IsRecorded(string line) => shipped.Contains(line) || unshipped.Contains(line);

    /// <summary>The shipped lines of a kind that were not removed on purpose.</summary>
    /// <param name="kind">The kind id.</param>
    /// <returns>The lines, sorted so the reports are stable.</returns>
    public IEnumerable<string> ShippedOf(string kind) =>
        shipped.Where(line => line.StartsWith(kind + " ", StringComparison.Ordinal) && !removed.Contains(line)).OrderBy(line => line, StringComparer.Ordinal);
}
