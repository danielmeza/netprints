#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Projects;

namespace NetPrints.Compilation;

/// <summary>
/// Maps build/compiler output to <see cref="CodeDiagnostic"/> (compilation-and-diagnostics.md §1).
/// <see cref="FromBuild"/> covers <see cref="IProjectSystem.BuildAsync"/>'s <see cref="ProjectMessage"/>s
/// directly (T061); mapping a generated-file message through a class's source map, and
/// <c>FromRoslyn</c>/<c>FromTranslation</c>, are added in T090 once the translator's <c>SourceMap</c>
/// exists.
/// </summary>
public static class DiagnosticMapper
{
    /// <summary>Maps every message from <see cref="IProjectSystem.BuildAsync"/> to a <see cref="CodeDiagnostic"/>.</summary>
    public static IReadOnlyList<CodeDiagnostic> FromBuild(IReadOnlyList<ProjectMessage> messages) =>
        messages.Select(FromMessage).ToList();

    private static CodeDiagnostic FromMessage(ProjectMessage message) =>
        new(ToSeverity(message.Severity), message.Code, message.Message, null, null, null, message.File, ToSpan(message));

    private static LinePositionSpan? ToSpan(ProjectMessage message)
    {
        if (message.Line is not int line)
        {
            return null;
        }

        var position = new LinePosition(line - 1, (message.Column ?? 1) - 1);
        return new LinePositionSpan(position, position);
    }

    private static CodeDiagnosticSeverity ToSeverity(ProjectMessageSeverity severity) => severity switch
    {
        ProjectMessageSeverity.Error => CodeDiagnosticSeverity.Error,
        ProjectMessageSeverity.Warning => CodeDiagnosticSeverity.Warning,
        _ => CodeDiagnosticSeverity.Info,
    };
}
