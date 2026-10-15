namespace NetPrints.Editor.Output;

/// <summary>Where a line of the Output panel came from.</summary>
public enum OutputLineKind
{
    /// <summary>The build: its start, its diagnostics and its result.</summary>
    Build,

    /// <summary>The program's standard output.</summary>
    Output,

    /// <summary>The program's standard error.</summary>
    Error,
}
