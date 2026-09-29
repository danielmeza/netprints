#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Translator;

namespace NetPrints.Compilation;

/// <summary>
/// Maps build/compiler output to <see cref="CodeDiagnostic"/> (compilation-and-diagnostics.md §1).
/// <see cref="FromBuild"/> covers every <see cref="IProjectSystem.BuildAsync"/> message (T061): a
/// generator message's <c>(graph &lt;key&gt;, node &lt;id&gt;)</c> suffix
/// (<c>NetPrints.Workspace.MsBuildMessageParser</c>) is parsed and stripped, and a message against a
/// generated <c>*.netpc.g.cs</c> file with no such suffix (a genuine compiler diagnostic) is mapped
/// through that class's <see cref="SourceMap"/> when the caller supplies one (RC-T10). <see cref="FromRoslyn"/>
/// and <see cref="FromTranslation"/> map a live <c>CodeAnalysisSession</c> diagnostic and a translation
/// failure the same way.
/// </summary>
public static partial class DiagnosticMapper
{
    [GeneratedRegex(@"^(?<message>.*) \(graph (?<key>[^,]+), node (?<id>.+)\)$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex GraphSuffixPattern();

    /// <summary>Maps every message from <see cref="IProjectSystem.BuildAsync"/> to a <see cref="CodeDiagnostic"/>.</summary>
    /// <param name="messages">Messages to map, in build order.</param>
    /// <param name="classesByGeneratedPath">Each class whose generated file a message might be
    /// reported against (its full path, as <see cref="ProjectMessage.File"/> carries it) together with
    /// its <see cref="ClassGraph"/> and last <see cref="TranslatedClass"/>, so a compiler message with
    /// only a line/column can be mapped back to a node through its <see cref="SourceMap"/>. Omitted (or
    /// missing an entry) when the caller has none available yet, in which case such a message keeps no
    /// <see cref="CodeDiagnostic.GraphKey"/>/<see cref="CodeDiagnostic.NodeId"/>.</param>
    /// <returns>One diagnostic per message, in the same order.</returns>
    public static IReadOnlyList<CodeDiagnostic> FromBuild(IReadOnlyList<ProjectMessage> messages,
        IReadOnlyDictionary<string, (ClassGraph Class, TranslatedClass Translated)>? classesByGeneratedPath = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages.Select(message => FromMessage(message, classesByGeneratedPath)).ToList();
    }

    /// <summary>
    /// Maps a live <c>CodeAnalysisSession</c> diagnostic (compilation-and-diagnostics.md §3) to a
    /// <see cref="CodeDiagnostic"/>, resolving its node through <paramref name="map"/> when one is given.
    /// </summary>
    /// <param name="diagnostic">Diagnostic to map.</param>
    /// <param name="cls">Class the diagnostic's source belongs to, if known.</param>
    /// <param name="map">Source map of the class the diagnostic's source belongs to, if known.</param>
    /// <returns>The mapped diagnostic.</returns>
    public static CodeDiagnostic FromRoslyn(Diagnostic diagnostic, ClassGraph? cls, SourceMap? map)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        bool inSource = diagnostic.Location.IsInSource;
        FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();

        (string? graphKey, string? nodeId) = inSource && map is not null && map.Find(diagnostic.Location.SourceSpan.Start) is { } entry
            ? (entry.GraphKey, entry.NodeId)
            : (null, null);

        return new CodeDiagnostic(
            ToSeverity(diagnostic.Severity),
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            cls?.FullName,
            graphKey,
            nodeId,
            inSource ? lineSpan.Path : null,
            inSource ? lineSpan.Span : null);
    }

    /// <summary>Maps a translation failure (compilation-and-diagnostics.md §2) to a <see cref="CodeDiagnostic"/>.</summary>
    /// <param name="exception">Failure to map.</param>
    /// <param name="cls">Class being translated when <paramref name="exception"/> was thrown.</param>
    /// <returns>The mapped diagnostic.</returns>
    public static CodeDiagnostic FromTranslation(TranslationException exception, ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(cls);

        return new CodeDiagnostic(CodeDiagnosticSeverity.Error, exception.Code, exception.Message,
            cls.FullName, exception.GraphKey, exception.NodeId, null, null);
    }

    private static CodeDiagnostic FromMessage(ProjectMessage message,
        IReadOnlyDictionary<string, (ClassGraph Class, TranslatedClass Translated)>? classesByGeneratedPath)
    {
        (string text, string? graphKey, string? nodeId) = ExtractGraphSuffix(message.Message);
        CodeDiagnostic diagnostic = new(ToSeverity(message.Severity), message.Code, text, null, graphKey, nodeId, message.File, ToSpan(message));

        if (diagnostic.GraphKey is null && message.File is { } file && message.Line is int line
            && classesByGeneratedPath is not null && classesByGeneratedPath.TryGetValue(file, out (ClassGraph Class, TranslatedClass Translated) generated)
            && ToPosition(generated.Translated.Code, line, message.Column ?? 1) is int position
            && generated.Translated.Map.Find(position) is { } entry)
        {
            diagnostic = diagnostic with { ClassFullName = generated.Class.FullName, GraphKey = entry.GraphKey, NodeId = entry.NodeId };
        }

        return diagnostic;
    }

    private static (string Message, string? GraphKey, string? NodeId) ExtractGraphSuffix(string message)
    {
        Match match = GraphSuffixPattern().Match(message);
        return match.Success
            ? (match.Groups["message"].Value, match.Groups["key"].Value, match.Groups["id"].Value)
            : (message, null, null);
    }

    private static int? ToPosition(string code, int line, int column)
    {
        TextLineCollection lines = SourceText.From(code).Lines;
        return line >= 1 && line <= lines.Count ? lines[line - 1].Start + Math.Max(0, column - 1) : null;
    }

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

    private static CodeDiagnosticSeverity ToSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => CodeDiagnosticSeverity.Error,
        DiagnosticSeverity.Warning => CodeDiagnosticSeverity.Warning,
        _ => CodeDiagnosticSeverity.Info,
    };
}
