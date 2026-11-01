using System.Globalization;

namespace NetPrints.Workspace.Run;

/// <summary>One diagnostic found while reading or resolving a launch profile.</summary>
/// <param name="Code">The stable <c>NPR</c> code, see <see cref="NprDiagnosticCodes"/>.</param>
/// <param name="Severity">Whether the run can go on.</param>
/// <param name="Message">The human-readable message, without the code.</param>
public sealed record RunProblem(string Code, RunProblemSeverity Severity, string Message)
{
    /// <summary>The problem as one canonical line, such as <c>error NPR005: The variable GODOT4 is not set (executablePath of profile 'Game').</c>.</summary>
    /// <returns>The line, with no trailing newline.</returns>
    public string ToLine()
    {
        string severity = Severity == RunProblemSeverity.Error ? "error" : "warning";
        return string.Create(CultureInfo.InvariantCulture, $"{severity} {Code}: {Message}");
    }
}
