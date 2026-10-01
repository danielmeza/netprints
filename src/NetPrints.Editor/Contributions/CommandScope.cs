namespace NetPrints.Editor.Contributions;

/// <summary>Where a command's gestures are active.</summary>
public enum CommandScope
{
    /// <summary>Shell-wide; overlaps every other scope.</summary>
    Global,

    /// <summary>Only while the graph canvas has focus.</summary>
    Graph,

    /// <summary>Only while the project tree has focus.</summary>
    ProjectTree,
}
