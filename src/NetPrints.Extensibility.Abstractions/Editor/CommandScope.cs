using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>Where a command's gestures are active; a command may name several scopes (<c>Graph | ProjectTree</c>).</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
[Flags]
public enum CommandScope
{
    /// <summary>Shell-wide; overlaps every other scope.</summary>
    Global = 0,

    /// <summary>Only while the graph canvas has focus.</summary>
    Graph = 1,

    /// <summary>Only while the project tree has focus.</summary>
    ProjectTree = 2,

    /// <summary>Only while the Variables panel's lists have focus; the command acts on the selected row, passed as the command parameter.</summary>
    VariablesPanel = 4,
}
