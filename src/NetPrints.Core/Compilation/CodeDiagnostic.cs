#nullable enable
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Compilation;

/// <summary>
/// The text of one source file passed to Roslyn analysis alongside a project's references
/// (compilation-and-diagnostics.md §1): a generated <c>.netpc.g.cs</c>, another <c>Compile</c> item, or
/// an <c>obj/</c>-generated file such as a global usings file. Pulled forward from sub-phase I's
/// <c>DiagnosticMapper</c>/<c>SourceMap</c> section (T090) because <c>NetPrints.Projects.ProjectSnapshot</c>
/// (project-system.md §4, T050) already needs it for <c>OtherSources</c> — see implementation-notes.md.
/// </summary>
/// <param name="Path">Full path of the source file.</param>
/// <param name="Text">The file's full text.</param>
public sealed record SourceFile(string Path, string Text);

/// <summary>
/// Severity of a <see cref="CodeDiagnostic"/>.
/// </summary>
public enum CodeDiagnosticSeverity
{
    /// <summary>Informational.</summary>
    Info,

    /// <summary>A warning: the build or analysis still succeeded.</summary>
    Warning,

    /// <summary>An error: the build or analysis failed.</summary>
    Error,
}

/// <summary>
/// A structured diagnostic row shown in the editor's error list and used for build messages
/// (compilation-and-diagnostics.md §1): a Roslyn compiler diagnostic, a translation error, a document
/// format issue (see <c>NetPrints.Serialization.DiagnosticExtensions.ToDiagnostic</c>), or an
/// MSBuild build message, normalized to one shape.
/// </summary>
/// <param name="Severity">How serious the diagnostic is.</param>
/// <param name="Id">Stable machine-readable code (e.g. <c>"CS0103"</c>, <c>"NPT001"</c>,
/// <c>"NPD002"</c>, <c>"NU1101"</c>, <c>"MSB3073"</c>).</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="ClassFullName">Full name of the class the diagnostic belongs to, if known.</param>
/// <param name="GraphKey">Graph key (<c>NetPrints.Core.GraphKeys.For</c>) of the graph the diagnostic
/// maps to, if known.</param>
/// <param name="NodeId">Id of the node the diagnostic maps to, if known.</param>
/// <param name="SourcePath">Path of the source file the diagnostic was reported against, if known.</param>
/// <param name="Span">0-based line/column span of the diagnostic in its source file, if known.</param>
public sealed record CodeDiagnostic(
    CodeDiagnosticSeverity Severity,
    string Id,
    string Message,
    string? ClassFullName,
    string? GraphKey,
    string? NodeId,
    string? SourcePath,
    LinePositionSpan? Span);
