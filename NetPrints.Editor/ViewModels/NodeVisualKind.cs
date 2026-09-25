namespace NetPrints.Editor.ViewModels;

/// <summary>
/// Visual category of a node; the view maps it to the header color.
/// </summary>
public enum NodeVisualKind
{
    Default,
    Entry,
    Return,
    CallMethod,
    CallStatic,
    Constructor,
    MakeDelegate,
    Type,
    VariableGetter,
    VariableSetter,
    MakeArray,
    Throw,
    Ternary,
}
