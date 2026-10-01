using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// The rules <see cref="SourceHygieneTests"/> applies to MSBuild files (ADR-0003, ADR-0017). They read the files as XML and
    /// match elements by local name, so attribute order, quote style, a <c>Condition</c> or a namespace cannot hide an entry.
    /// </summary>
    internal static class BuildFileRules
    {
        internal const string OptInTargetsFile = "Directory.Build.targets";

        internal const string OptInNoWarnValue = "$(NoWarn);@(NetPrintsExperimentalOptIn)";

        internal const string OptInItem = "NetPrintsExperimentalOptIn";

        /// <summary>ADR-0003 "Build-property allowances": engine-level warning suppression allowed for one code in one file.</summary>
        private static readonly HashSet<(string File, string Code)> BuildPropertyAllowlist = new()
        {
            // MinVer warns (MINVER1001) when there is no .git history (a source archive); release
            // contract §1 requires the build to stay green in that case.
            ("Directory.Build.props", "MINVER1001"),
        };

        /// <summary>The build files matching <paramref name="patterns"/>, except build output and <c>legacy/</c> (kept for reference, not built).</summary>
        internal static IEnumerable<string> EnumeratePaths(string root, params string[] patterns) =>
            patterns.SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}legacy{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        internal static IEnumerable<(string RelativePath, XDocument Document)> Load(string root, params string[] patterns) =>
            EnumeratePaths(root, patterns).Select(path => (Path.GetRelativePath(root, path).Replace('\\', '/'), XDocument.Load(path)));

        internal static IEnumerable<XElement> Named(XDocument document, string localName) =>
            document.Descendants().Where(element => element.Name.LocalName == localName);

        private static string? Attribute(XElement element, string localName) =>
            element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

        /// <summary>The ids of every <c>NetPrintsExperimentalOptIn</c> item, whether it uses <c>Include</c> or <c>Update</c>.</summary>
        internal static IEnumerable<string> OptInIds(XDocument document) =>
            Named(document, OptInItem)
                .SelectMany(item => new[] { Attribute(item, "Include"), Attribute(item, "Update") })
                .OfType<string>()
                .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        /// <summary>ADR-0017: the one <c>&lt;NoWarn&gt;</c> the repository allows is the bare opt-in line in the root <c>Directory.Build.targets</c>.</summary>
        internal static bool IsOptInNoWarn(string relativePath, XDocument document, XElement noWarn) =>
            relativePath == OptInTargetsFile
            && !noWarn.HasAttributes
            && noWarn.Value.Trim() == OptInNoWarnValue
            && Named(document, "NoWarn").Count() == 1;

        /// <summary>Every <c>&lt;NoWarn&gt;</c> element of a file, whatever its attributes.</summary>
        internal static IEnumerable<XElement> NoWarnElements(XDocument document) => Named(document, "NoWarn");

        /// <summary>
        /// The ways a build file lowers the warning bar or hides a diagnostic: a <c>NoWarn</c> other than the opt-in line,
        /// <c>WarningsNotAsErrors</c>, <c>TreatWarningsAsErrors=false</c>, <c>WarningLevel</c>, an unlisted engine-level
        /// suppression, another analyzer config file (<c>GlobalAnalyzerConfigFiles</c>, <c>EditorConfigFiles</c>) or an
        /// <c>&lt;Analyzer Remove&gt;</c> item.
        /// </summary>
        internal static IReadOnlyList<string> WarningOffenders(string relativePath, XDocument document)
        {
            var offenders = new List<string>();

            foreach (XElement noWarn in NoWarnElements(document).Where(element => !IsOptInNoWarn(relativePath, document, element)))
            {
                offenders.Add($"{relativePath}: <NoWarn>");
            }

            AddIfAny(offenders, relativePath, document, "WarningsNotAsErrors");
            AddIfAny(offenders, relativePath, document, "WarningLevel");
            AddIfAny(offenders, relativePath, document, "GlobalAnalyzerConfigFiles");
            AddIfAny(offenders, relativePath, document, "EditorConfigFiles");

            foreach (XElement element in Named(document, "TreatWarningsAsErrors").Where(element => string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add($"{relativePath}: <TreatWarningsAsErrors>false</TreatWarningsAsErrors>");
            }

            foreach (XElement element in Named(document, "Analyzer").Where(element => Attribute(element, "Remove") is not null))
            {
                offenders.Add($"{relativePath}: <Analyzer Remove>");
            }

            foreach (string name in new[] { "MSBuildWarningsNotAsErrors", "MSBuildWarningsAsMessages" })
            {
                foreach (string code in Named(document, name).SelectMany(element => ExtractWarningCodes(element.Value)))
                {
                    if (!BuildPropertyAllowlist.Contains((relativePath, code)))
                    {
                        offenders.Add($"{relativePath}: unlisted <{name}> {code}");
                    }
                }
            }

            return offenders;
        }

        private static void AddIfAny(List<string> offenders, string relativePath, XDocument document, string localName)
        {
            if (Named(document, localName).Any())
            {
                offenders.Add($"{relativePath}: <{localName}>");
            }
        }

        /// <summary>Splits a semicolon-separated MSBuild property value into its literal warning codes, dropping a self-referencing <c>$(...)</c> expansion.</summary>
        private static IEnumerable<string> ExtractWarningCodes(string value) =>
            value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !token.StartsWith("$(", StringComparison.Ordinal));
    }
}
