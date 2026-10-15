using System.Collections.Frozen;
using NetPrints.Editor.Icons;

namespace NetPrints.Editor.Graph.Nodes;

/// <summary>The glyph each node header shows for its <see cref="NodeVisualKind"/> (FR-086).</summary>
public static class NodeIcons
{
    private static readonly FrozenDictionary<NodeVisualKind, string> Glyphs = new Dictionary<NodeVisualKind, string>
    {
        [NodeVisualKind.Default] = IconIds.NodeKindDefault,
        [NodeVisualKind.Entry] = IconIds.NodeKindEntry,
        [NodeVisualKind.Return] = IconIds.NodeKindReturn,
        [NodeVisualKind.CallMethod] = IconIds.NodeKindCallMethod,
        [NodeVisualKind.CallStatic] = IconIds.NodeKindCallStatic,
        [NodeVisualKind.Constructor] = IconIds.NodeKindConstructor,
        [NodeVisualKind.MakeDelegate] = IconIds.NodeKindMakeDelegate,
        [NodeVisualKind.Type] = IconIds.NodeKindType,
        [NodeVisualKind.VariableGetter] = IconIds.NodeKindVariableGetter,
        [NodeVisualKind.VariableSetter] = IconIds.NodeKindVariableSetter,
        [NodeVisualKind.MakeArray] = IconIds.NodeKindMakeArray,
        [NodeVisualKind.Throw] = IconIds.NodeKindThrow,
        [NodeVisualKind.Ternary] = IconIds.NodeKindTernary,
        [NodeVisualKind.IfElse] = IconIds.NodeKindIfElse,
        [NodeVisualKind.ForLoop] = IconIds.NodeKindForLoop,
        [NodeVisualKind.ExplicitCast] = IconIds.NodeKindExplicitCast,
        [NodeVisualKind.Await] = IconIds.NodeKindAwait,
    }.ToFrozenDictionary();

    /// <summary>Gets the icon id of a node kind.</summary>
    /// <param name="kind">The node's visual kind.</param>
    /// <returns>The kind's icon id, or the default node glyph for a kind with none.</returns>
    public static string For(NodeVisualKind kind) => Glyphs.GetValueOrDefault(kind, IconIds.NodeKindDefault);
}
