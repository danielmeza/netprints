#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NetPrints.Projects;

namespace NetPrints.Workspace;

/// <summary>
/// Parses build output written in MSBuild's canonical message format into <see cref="ProjectMessage"/>s
/// (project-system.md §4.1): compiler (<c>CS…</c>), NuGet (<c>NU…</c>), MSBuild (<c>MSB…</c>) and
/// NetPrints (<c>NPT…</c>/<c>NPD…</c>) diagnostics alike.
/// </summary>
public static partial class MsBuildMessageParser
{
    [GeneratedRegex(
        @"^(?<file>.+?)(\((?<line>\d+)(,(?<col>\d+))?\))?\s*:\s*(?<sev>error|warning|info)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<msg>.*?)(\s+\[[^\]]+\])?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalMessagePattern();

    /// <summary>
    /// Parses every line of <paramref name="output"/> that is in MSBuild's canonical message format;
    /// lines that don't match it are ignored. The <c>(graph &lt;key&gt;, node &lt;id&gt;)</c> suffix a
    /// NetPrints generator message adds to its own text (project-system.md §3) is kept as part of
    /// <see cref="ProjectMessage.Message"/> here; <c>DiagnosticMapper</c> extracts it later.
    /// </summary>
    /// <param name="output">A build's combined stdout and stderr output.</param>
    /// <returns>The parsed messages, in the order they appear in <paramref name="output"/>.</returns>
    public static IReadOnlyList<ProjectMessage> Parse(string output)
    {
        var messages = new List<ProjectMessage>();

        foreach (Match match in CanonicalMessagePattern().Matches(output))
        {
            ProjectMessageSeverity severity = match.Groups["sev"].Value switch
            {
                "error" => ProjectMessageSeverity.Error,
                "warning" => ProjectMessageSeverity.Warning,
                _ => ProjectMessageSeverity.Info,
            };

            int? line = ParseGroup(match.Groups["line"]);
            int? column = ParseGroup(match.Groups["col"]);

            messages.Add(new ProjectMessage(
                severity,
                match.Groups["code"].Value,
                match.Groups["msg"].Value,
                match.Groups["file"].Value,
                line,
                column));
        }

        return messages;
    }

    private static int? ParseGroup(Group group) =>
        group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : null;
}
