using System.Collections.Frozen;

namespace NetPrints.Editor.Graph.Nodes;

/// <summary>
/// The role that colours a node's header (FR-086). A role has one style class, <c>role-&lt;name&gt;</c>, and one
/// <c>Node.Header.&lt;Name&gt;</c> theme token in each variant.
/// </summary>
public sealed class NodeRole
{
    private NodeRole(string name)
    {
        Name = name;
        StyleClass = "role-" + name.ToLowerInvariant();
    }

    /// <summary>Method entries, event entries and return nodes.</summary>
    public static NodeRole Entry { get; } = new("Entry");

    /// <summary>Impure calls.</summary>
    public static NodeRole Call { get; } = new("Call");

    /// <summary>Pure nodes that have no role of their own.</summary>
    public static NodeRole Pure { get; } = new("Pure");

    /// <summary>Nodes with execution pins that have no role of their own, such as branches and loops.</summary>
    public static NodeRole Flow { get; } = new("Flow");

    /// <summary>Variable getters and setters.</summary>
    public static NodeRole Variable { get; } = new("Variable");

    /// <summary>Constructor calls.</summary>
    public static NodeRole Constructor { get; } = new("Constructor");

    /// <summary>Calls whose method returns <see cref="Task"/>, <see cref="ValueTask"/> or a generic form of either.</summary>
    public static NodeRole Async { get; } = new("Async");

    /// <summary>Throw nodes.</summary>
    public static NodeRole Throw { get; } = new("Throw");

    /// <summary>Every role, in the order of the spec.</summary>
    public static IReadOnlyList<NodeRole> All { get; } = [Entry, Call, Pure, Flow, Variable, Constructor, Async, Throw];

    private static FrozenDictionary<NodeVisualKind, NodeRole> FixedRoles { get; } = new Dictionary<NodeVisualKind, NodeRole>
    {
        [NodeVisualKind.Entry] = Entry,
        [NodeVisualKind.Return] = Entry,
        [NodeVisualKind.Constructor] = Constructor,
        [NodeVisualKind.VariableGetter] = Variable,
        [NodeVisualKind.VariableSetter] = Variable,
        [NodeVisualKind.Throw] = Throw,
        [NodeVisualKind.MakeDelegate] = Pure,
        [NodeVisualKind.Type] = Pure,
        [NodeVisualKind.MakeArray] = Pure,
        [NodeVisualKind.Ternary] = Pure,
    }.ToFrozenDictionary();

    /// <summary>The role's name, which is also the suffix of its <c>Node.Header.&lt;Name&gt;</c> token.</summary>
    public string Name { get; }

    /// <summary>The style class that selects the role's header colour.</summary>
    public string StyleClass { get; }

    /// <summary>Gets the role of a node.</summary>
    /// <param name="kind">The node's visual kind; a kind the table does not list gets its role by convention.</param>
    /// <param name="isPure">Whether the node is pure.</param>
    /// <param name="hasExecPins">Whether the node has execution pins.</param>
    /// <param name="returnsTask">Whether the node calls a method that returns <see cref="Task"/>, <see cref="ValueTask"/> or a generic form of either.</param>
    /// <returns>The role: never a default one. A node with execution pins is <see cref="Flow"/>, any other is <see cref="Pure"/>.</returns>
    public static NodeRole Resolve(NodeVisualKind kind, bool isPure, bool hasExecPins, bool returnsTask)
    {
        if (FixedRoles.TryGetValue(kind, out NodeRole? fixedRole))
        {
            return fixedRole;
        }

        if (kind is NodeVisualKind.CallMethod or NodeVisualKind.CallStatic)
        {
            return returnsTask ? Async : isPure ? Pure : Call;
        }

        return hasExecPins ? Flow : Pure;
    }
}
