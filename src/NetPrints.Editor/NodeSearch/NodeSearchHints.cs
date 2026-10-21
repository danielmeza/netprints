using NetPrints.Extensibility.Nodes;
using NetPrints.Reflection;

namespace NetPrints.Editor.NodeSearch;

/// <summary>Gives the editor's entries the title and keywords a catalog's node hints describe (AB-10).</summary>
internal static class NodeSearchHints
{
    /// <summary>Applies the hint of <paramref name="entry"/>, when a catalog has one.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="hints">The catalogs' hints, or <see langword="null"/> when the reflection provider has none.</param>
    /// <returns>The entry with the hint's display name as its text and its keywords; <paramref name="entry"/> itself when there is nothing to apply.</returns>
    public static NodeSearchEntry Apply(NodeSearchEntry entry, INodeHintSource? hints)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (hints is null || entry is not EditorSearchEntry editorEntry || editorEntry.HintFrom(hints) is not { } hint)
        {
            return entry;
        }

        bool named = !string.IsNullOrWhiteSpace(hint.DisplayName);
        if (!named && hint.Keywords.Count == 0)
        {
            return entry;
        }

        return entry with
        {
            Text = named ? hint.DisplayName ?? entry.Text : entry.Text,
            Keywords = hint.Keywords.Count > 0 ? hint.Keywords : entry.Keywords,
        };
    }
}
