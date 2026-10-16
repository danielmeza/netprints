using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Authoring.Localization;

/// <summary>One <c>data</c> entry of a resx file.</summary>
/// <param name="Value">The entry's text.</param>
/// <param name="Location">The line of the entry in the file.</param>
internal sealed record ResxEntry(string Value, Location Location);

/// <summary>A resx file among the additional files: its entries, and whether it is the neutral file or a satellite of one.</summary>
internal sealed class ResxFile
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex CulturePattern = new("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex PlaceholderPattern = new(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.CultureInvariant, MatchTimeout);

    private ResxFile(string path, string directory, string baseName, bool isNeutral, Dictionary<string, ResxEntry> entries)
    {
        Path = path;
        Directory = directory;
        BaseName = baseName;
        IsNeutral = isNeutral;
        Entries = entries;
    }

    /// <summary>Gets the path of the file.</summary>
    public string Path { get; }

    /// <summary>Gets the directory of the file.</summary>
    public string Directory { get; }

    /// <summary>Gets the file name without the culture and the extension, which a satellite shares with its neutral file.</summary>
    public string BaseName { get; }

    /// <summary>Gets a value indicating whether the file has no culture in its name.</summary>
    public bool IsNeutral { get; }

    /// <summary>Gets the entries by key.</summary>
    public IReadOnlyDictionary<string, ResxEntry> Entries { get; }

    /// <summary>Reads the resx files among the additional files; a file that is not well-formed is left out.</summary>
    /// <param name="files">The additional files.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The files.</returns>
    public static List<ResxFile> ReadAll(IEnumerable<AdditionalText> files, CancellationToken token)
    {
        List<ResxFile> result = [];
        foreach (AdditionalText file in files.Where(item => item.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
        {
            SourceText? text = file.GetText(token);
            if (text is null)
            {
                continue;
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(file.Path);
            int dot = name.LastIndexOf('.');
            bool isSatellite = dot > 0 && CulturePattern.IsMatch(name.Substring(dot + 1));
            Dictionary<string, ResxEntry>? entries = Parse(file.Path, text);
            if (entries is not null)
            {
                result.Add(new ResxFile(file.Path, System.IO.Path.GetDirectoryName(file.Path) ?? string.Empty, isSatellite ? name.Substring(0, dot) : name, !isSatellite, entries));
            }
        }

        return result;
    }

    /// <summary>Counts the distinct <c>{n}</c> placeholders of a resource text.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The number of distinct indices.</returns>
    public static int PlaceholderCount(string value) =>
        PlaceholderPattern.Matches(value).Cast<Match>().Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Count();

    /// <summary>Finds the neutral file a satellite translates.</summary>
    /// <param name="all">Every resx file of the project.</param>
    /// <returns>The neutral file, or null.</returns>
    public ResxFile? NeutralIn(IEnumerable<ResxFile> all) =>
        all.FirstOrDefault(other => other.IsNeutral
            && string.Equals(other.Directory, Directory, StringComparison.Ordinal)
            && string.Equals(other.BaseName, BaseName, StringComparison.Ordinal));

    private static Dictionary<string, ResxEntry>? Parse(string path, SourceText text)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(text.ToString(), LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }

        Dictionary<string, ResxEntry> entries = new(StringComparer.Ordinal);
        foreach (XElement data in document.Descendants("data"))
        {
            string? key = (string?)data.Attribute("name");
            if (key is null || entries.ContainsKey(key))
            {
                continue;
            }

            int line = ((IXmlLineInfo)data).LineNumber - 1;
            TextSpan span = line >= 0 && line < text.Lines.Count ? text.Lines[line].Span : TextSpan.FromBounds(0, 0);
            entries[key] = new ResxEntry((string?)data.Element("value") ?? string.Empty, Location.Create(path, span, text.Lines.GetLinePositionSpan(span)));
        }

        return entries;
    }
}
