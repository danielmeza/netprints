namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// Kind of a pin; the view maps it to shape (exec = square, data = circle, type = triangle) and color.
/// </summary>
public enum PinKind
{
    /// <summary>An execution pin, drawn as a square (<see cref="NetPrints.Graph.NodeExecPin"/>).</summary>
    Exec,

    /// <summary>A data pin, drawn as a circle (<see cref="NetPrints.Graph.NodeDataPin"/>).</summary>
    Data,

    /// <summary>A type pin, drawn as a triangle (<see cref="NetPrints.Graph.NodeTypePin"/>).</summary>
    Type,
}
