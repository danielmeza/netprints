namespace NetPrints.Editor.Graph.Nodes;

/// <summary>
/// Visual category of a node; the view maps it to the header color.
/// </summary>
public enum NodeVisualKind
{
    /// <summary>Any node type not otherwise listed here.</summary>
    Default,

    /// <summary>An <see cref="NetPrints.Graph.ExecutionEntryNode"/>.</summary>
    Entry,

    /// <summary>A <see cref="NetPrints.Graph.ReturnNode"/>.</summary>
    Return,

    /// <summary>A <see cref="NetPrints.Graph.CallMethodNode"/> calling an instance method.</summary>
    CallMethod,

    /// <summary>A <see cref="NetPrints.Graph.CallMethodNode"/> calling a static method.</summary>
    CallStatic,

    /// <summary>A <see cref="NetPrints.Graph.ConstructorNode"/>.</summary>
    Constructor,

    /// <summary>A <see cref="NetPrints.Graph.MakeDelegateNode"/>.</summary>
    MakeDelegate,

    /// <summary>A <see cref="NetPrints.Graph.TypeNode"/> or <see cref="NetPrints.Graph.MakeArrayTypeNode"/>.</summary>
    Type,

    /// <summary>A <see cref="NetPrints.Graph.VariableGetterNode"/>.</summary>
    VariableGetter,

    /// <summary>A <see cref="NetPrints.Graph.VariableSetterNode"/>.</summary>
    VariableSetter,

    /// <summary>A <see cref="NetPrints.Graph.MakeArrayNode"/>.</summary>
    MakeArray,

    /// <summary>A <see cref="NetPrints.Graph.ThrowNode"/>.</summary>
    Throw,

    /// <summary>A <see cref="NetPrints.Graph.TernaryNode"/>.</summary>
    Ternary,
}
