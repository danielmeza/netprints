namespace NetPrints.Editor.Output;

/// <summary>One line of the Output panel and where it came from.</summary>
/// <param name="Kind">The source of the line.</param>
/// <param name="Text">The text, without its line ending.</param>
public sealed record OutputLineViewModel(OutputLineKind Kind, string Text)
{
    /// <summary>Gets a value indicating whether the line came from the program's standard error.</summary>
    public bool IsError => Kind == OutputLineKind.Error;
}
