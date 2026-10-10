namespace NetPrints.Editor.Hosting;

/// <summary>Which of a started process's output streams a line came from.</summary>
public enum ProcessStream
{
    /// <summary>Standard output.</summary>
    Output,

    /// <summary>Standard error.</summary>
    Error,
}
