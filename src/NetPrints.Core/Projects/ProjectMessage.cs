#nullable enable

namespace NetPrints.Projects;

/// <summary>
/// Severity of a <see cref="ProjectMessage"/>.
/// </summary>
public enum ProjectMessageSeverity
{
    /// <summary>Informational.</summary>
    Info,

    /// <summary>A warning: loading or building still succeeded.</summary>
    Warning,

    /// <summary>An error: loading or building failed, or partially failed.</summary>
    Error,
}

/// <summary>
/// One message produced while loading or building a project (project-system.md §4): a restore or
/// evaluation problem, an <c>MSBuildWorkspace</c> diagnostic, or a compiler/NuGet/MSBuild message
/// parsed from a build's output by <c>NetPrints.Workspace.MsBuildMessageParser</c>.
/// </summary>
/// <param name="Severity">How serious the message is.</param>
/// <param name="Code">Stable machine-readable code: one of this type's own <c>NPW…</c> constants, or an
/// external code such as <c>"CS0103"</c>, <c>"NU1101"</c>, <c>"MSB3073"</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="File">Path of the file the message applies to, if any.</param>
/// <param name="Line">1-based line number the message applies to, if any.</param>
/// <param name="Column">1-based column number the message applies to, if any.</param>
public sealed record ProjectMessage(
    ProjectMessageSeverity Severity,
    string Code,
    string Message,
    string? File,
    int? Line,
    int? Column)
{
    /// <summary>
    /// Restoring the project out of process failed; <see cref="IProjectSystem.LoadAsync"/> continues
    /// with whatever was already restored (project-system.md §4).
    /// </summary>
    public const string RestoreFailed = "NPW002";

    /// <summary>
    /// The project multi-targets (<c>TargetFrameworks</c>); the editor uses the first target
    /// framework moniker (project-system.md §1).
    /// </summary>
    public const string MultiTargetingUsesFirstFramework = "NPW004";

    /// <summary>
    /// <c>MSBuildWorkspace.WorkspaceFailed</c> reported a diagnostic while opening the project
    /// (project-system.md §4).
    /// </summary>
    public const string WorkspaceDiagnostic = "NPW005";
}
