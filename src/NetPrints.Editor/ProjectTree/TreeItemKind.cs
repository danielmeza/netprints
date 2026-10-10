namespace NetPrints.Editor.ProjectTree;

/// <summary>What a project tree item stands for.</summary>
public enum TreeItemKind
{
    /// <summary>The open project.</summary>
    Project,

    /// <summary>A class of the project.</summary>
    Class,

    /// <summary>A group of a class: Methods, Constructors, Variables or Event graphs.</summary>
    Group,

    /// <summary>A method.</summary>
    Method,

    /// <summary>A constructor.</summary>
    Constructor,

    /// <summary>A variable.</summary>
    Variable,

    /// <summary>An event graph.</summary>
    EventGraph,
}
