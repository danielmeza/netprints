using System;
using System.Collections.Generic;
using System.Text;

namespace NetPrints.Cli.Git;

/// <summary>
/// The text of a <c>.gitattributes</c> file seen through the one line <c>git-install</c> manages (contracts/git.md §3): it finds lines that name
/// another driver for graphs, and adds, replaces or removes the tool's own line without touching the other lines or any line ending.
/// </summary>
internal sealed class GitAttributesFile
{
    /// <summary>The path pattern of graph files.</summary>
    public const string Pattern = "*.netpc.json";

    /// <summary>The name of the diff and merge drivers the tool registers.</summary>
    public const string DriverName = "netprints";

    /// <summary>Gets the line installed without the merge driver.</summary>
    public static string DiffLine { get; } = $"{Pattern} diff={DriverName}";

    /// <summary>Gets the line installed with the merge driver.</summary>
    public static string MergeLine { get; } = $"{Pattern} diff={DriverName} merge={DriverName}";

    // Latin-1 maps every byte to one char, so a file with any encoding round-trips unchanged.
    private static readonly Encoding Bytes = Encoding.Latin1;

    private readonly List<Line> _lines;

    private GitAttributesFile(List<Line> lines) => _lines = lines;

    /// <summary>Gets the first line that names a driver for graph files other than the tool's own, or <see langword="null"/> when there is none.</summary>
    public string? ConflictingLine
    {
        get
        {
            foreach (Line line in _lines)
            {
                if (Classify(line.Text) == Kind.Foreign)
                {
                    return line.Text.Trim();
                }
            }

            return null;
        }
    }

    /// <summary>Gets a value indicating whether the file has no content other than line terminators and blanks.</summary>
    public bool IsBlank => _lines.TrueForAll(line => string.IsNullOrWhiteSpace(line.Text));

    /// <summary>Reads the text of a file.</summary>
    /// <param name="content">The raw bytes of the file; empty for a file that does not exist yet.</param>
    /// <returns>The parsed file.</returns>
    public static GitAttributesFile Parse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        string text = Bytes.GetString(content);
        var lines = new List<Line>();
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
            {
                lines.Add(new Line(text[start..], string.Empty));
                break;
            }

            bool carriageReturn = end > start && text[end - 1] == '\r';
            lines.Add(new Line(text[start..(carriageReturn ? end - 1 : end)], carriageReturn ? "\r\n" : "\n"));
            start = end + 1;
        }

        return new GitAttributesFile(lines);
    }

    /// <summary>Checks whether the file already holds a tool line that covers what <paramref name="wantMerge"/> asks for.</summary>
    /// <param name="wantMerge">Whether the merge driver is wanted; a line with it also covers a request without it.</param>
    /// <returns><see langword="true"/> when nothing has to be written.</returns>
    public bool Covers(bool wantMerge) =>
        _lines.Exists(line => Classify(line.Text) == Kind.WithMerge) || (!wantMerge && _lines.Exists(line => Classify(line.Text) == Kind.DiffOnly));

    /// <summary>Checks whether the file holds a line the tool added.</summary>
    /// <returns><see langword="true"/> when an uninstall would change the file.</returns>
    public bool HasToolLine() => _lines.Exists(line => Classify(line.Text) is Kind.DiffOnly or Kind.WithMerge);

    /// <summary>Returns the file with the tool's line: the existing tool line is replaced in place, otherwise the line is appended.</summary>
    /// <param name="wantMerge">Whether the line carries the merge driver.</param>
    /// <returns>The new content.</returns>
    public byte[] WithLine(bool wantMerge)
    {
        string wanted = wantMerge ? MergeLine : DiffLine;
        var lines = new List<Line>(_lines);
        int existing = lines.FindIndex(line => Classify(line.Text) is Kind.DiffOnly or Kind.WithMerge);
        if (existing >= 0)
        {
            lines[existing] = lines[existing] with { Text = wanted };
            return Serialize(lines);
        }

        string terminator = "\n";
        foreach (Line line in lines)
        {
            if (line.Terminator.Length > 0)
            {
                terminator = line.Terminator;
            }
        }

        if (lines.Count > 0 && lines[^1].Terminator.Length == 0)
        {
            lines[^1] = lines[^1] with { Terminator = terminator };
        }

        lines.Add(new Line(wanted, terminator));
        return Serialize(lines);
    }

    /// <summary>Returns the file without the lines the tool added.</summary>
    /// <returns>The new content.</returns>
    public byte[] WithoutToolLines() =>
        Serialize(_lines.FindAll(line => Classify(line.Text) is not (Kind.DiffOnly or Kind.WithMerge)));

    private static byte[] Serialize(List<Line> lines)
    {
        var builder = new StringBuilder();
        foreach (Line line in lines)
        {
            builder.Append(line.Text).Append(line.Terminator);
        }

        return Bytes.GetBytes(builder.ToString());
    }

    private static Kind Classify(string text)
    {
        string[] tokens = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens[0] != Pattern)
        {
            return Kind.Unrelated;
        }

        string[] attributes = tokens[1..];
        if (attributes is [var only] && only == $"diff={DriverName}")
        {
            return Kind.DiffOnly;
        }

        if (attributes is [var diff, var merge] && diff == $"diff={DriverName}" && merge == $"merge={DriverName}")
        {
            return Kind.WithMerge;
        }

        foreach (string attribute in attributes)
        {
            bool driver = attribute is "diff" or "-diff" or "!diff" or "merge" or "-merge" or "!merge"
                || attribute.StartsWith("diff=", StringComparison.Ordinal)
                || attribute.StartsWith("merge=", StringComparison.Ordinal);
            if (driver && attribute != $"diff={DriverName}" && attribute != $"merge={DriverName}")
            {
                return Kind.Foreign;
            }
        }

        return Kind.Unrelated;
    }

    private enum Kind
    {
        Unrelated,
        DiffOnly,
        WithMerge,
        Foreign,
    }

    private sealed record Line(string Text, string Terminator);
}
