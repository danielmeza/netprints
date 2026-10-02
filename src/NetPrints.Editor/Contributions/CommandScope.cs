namespace NetPrints.Editor.Contributions;

/// <summary>Where a command's gestures are active; a command may name several scopes (<c>Graph | ProjectTree</c>).</summary>
[Flags]
public enum CommandScope
{
    /// <summary>Shell-wide; overlaps every other scope.</summary>
    Global = 0,

    /// <summary>Only while the graph canvas has focus.</summary>
    Graph = 1,

    /// <summary>Only while the project tree has focus.</summary>
    ProjectTree = 2,
}
