using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>Which side of the status bar an item sits on.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public enum StatusBarAlignment
{
    /// <summary>After the status message, on the left.</summary>
    Left,

    /// <summary>At the right edge.</summary>
    Right,
}
