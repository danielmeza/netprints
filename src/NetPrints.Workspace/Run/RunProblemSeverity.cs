namespace NetPrints.Workspace.Run;

/// <summary>How serious a <see cref="RunProblem"/> is.</summary>
public enum RunProblemSeverity
{
    /// <summary>The run goes on.</summary>
    Warning,

    /// <summary>The run cannot start.</summary>
    Error,
}
