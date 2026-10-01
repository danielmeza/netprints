namespace NetPrints.Editor.Contributions;

/// <summary>The item a context menu opens on.</summary>
public enum ContextMenuTarget
{
    /// <summary>A graph node.</summary>
    Node,

    /// <summary>A node pin.</summary>
    Pin,

    /// <summary>A connection between pins.</summary>
    Connection,

    /// <summary>The empty canvas.</summary>
    Canvas,

    /// <summary>A class in the project tree.</summary>
    TreeClass,

    /// <summary>A method, constructor or variable in the project tree.</summary>
    TreeMember,

    /// <summary>An event graph in the project tree.</summary>
    TreeEventGraph,
}
