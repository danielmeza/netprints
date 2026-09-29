namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// The kinds of graph a node kind may be used in (extension-points.md §2). Flags, so a descriptor can allow
/// several; bits above <see cref="Event"/> are free for graph kinds later phases add.
/// </summary>
[Flags]
public enum GraphKinds
{
    /// <summary>No graph.</summary>
    None = 0,

    /// <summary>A method graph.</summary>
    Method = 1,

    /// <summary>A constructor graph.</summary>
    Constructor = 2,

    /// <summary>A class-level graph.</summary>
    Class = 4,

    /// <summary>A type graph.</summary>
    Type = 8,

    /// <summary>An event graph.</summary>
    Event = 16,

    /// <summary>Every graph that runs code.</summary>
    Execution = Method | Constructor | Event,
}
