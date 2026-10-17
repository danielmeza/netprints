using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>
/// The reviewed exceptions to the hygiene rules, read from the <c>HygieneLedger.txt</c> additional file (ADR-0003). A row is
/// <c>CODE | path | detail</c>; a path matches the end of a file's full path, and a line that starts with <c>#</c> is a comment.
/// </summary>
internal sealed class HygieneLedger
{
    private const string FileName = "HygieneLedger.txt";

    private const char Separator = '|';

    private const int ColumnCount = 3;

    private readonly Dictionary<string, List<string>> pathsByKey;

    private HygieneLedger(Dictionary<string, List<string>> pathsByKey)
    {
        this.pathsByKey = pathsByKey;
    }

    /// <summary>Reads the ledger among the additional files of a compilation; no ledger is an empty one.</summary>
    /// <param name="files">The additional files.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ledger.</returns>
    public static HygieneLedger Read(ImmutableArray<AdditionalText> files, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> rows = new(StringComparer.Ordinal);
        foreach (AdditionalText file in files)
        {
            if (!file.Path.EndsWith(FileName, StringComparison.Ordinal))
            {
                continue;
            }

            SourceText? content = file.GetText(cancellationToken);
            if (content is null)
            {
                continue;
            }

            foreach (TextLine line in content.Lines)
            {
                string text = line.ToString().Trim();
                if (text.Length == 0 || text[0] == '#')
                {
                    continue;
                }

                string[] columns = text.Split(new[] { Separator }, ColumnCount);
                if (columns.Length != ColumnCount)
                {
                    continue;
                }

                string key = Key(columns[0].Trim(), columns[2].Trim());
                if (!rows.TryGetValue(key, out List<string>? paths))
                {
                    paths = [];
                    rows[key] = paths;
                }

                paths.Add(Normalize(columns[1].Trim()));
            }
        }

        return new HygieneLedger(rows);
    }

    /// <summary>Whether the ledger lists a site.</summary>
    /// <param name="code">The rule code.</param>
    /// <param name="filePath">The full path of the source file.</param>
    /// <param name="detail">The detail that names the site within the file.</param>
    /// <returns><see langword="true"/> when a row matches.</returns>
    public bool Contains(string code, string filePath, string detail)
    {
        if (!pathsByKey.TryGetValue(Key(code, detail), out List<string>? paths))
        {
            return false;
        }

        string normalized = Normalize(filePath);
        foreach (string path in paths)
        {
            if (normalized.Equals(path, StringComparison.Ordinal) || normalized.EndsWith("/" + path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Key(string code, string detail) => code + Separator + detail;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
