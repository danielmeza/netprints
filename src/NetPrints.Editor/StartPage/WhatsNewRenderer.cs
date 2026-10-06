using System.Text.RegularExpressions;

namespace NetPrints.Editor.StartPage;

/// <summary>Reads the release notes' Markdown subset (headings, bullets, paragraphs and links) into blocks, without a Markdown library.</summary>
internal static partial class WhatsNewRenderer
{
    private const int MaxHeadingLevel = 6;
    private const int BulletMarkerLength = 2;

    /// <summary>Parses the notes.</summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>The blocks in order.</returns>
    public static IReadOnlyList<WhatsNewBlock> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        List<WhatsNewBlock> blocks = [];
        List<string> paragraph = [];

        foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            string text = line.Trim();
            if (text.Length == 0)
            {
                Flush();
            }
            else if (HeadingLevel(text) is { } level)
            {
                Flush();
                blocks.Add(new WhatsNewHeading(level, Inlines(text[(level + 1)..].Trim())));
            }
            else if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
            {
                Flush();
                blocks.Add(new WhatsNewBullet(Inlines(text[BulletMarkerLength..].Trim())));
            }
            else
            {
                paragraph.Add(text);
            }
        }

        Flush();
        return blocks;

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new WhatsNewParagraph(Inlines(string.Join(' ', paragraph))));
                paragraph.Clear();
            }
        }
    }

    private static int? HeadingLevel(string line)
    {
        int level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        return level is >= 1 and <= MaxHeadingLevel && level < line.Length && line[level] == ' ' ? level : null;
    }

    private static List<WhatsNewSegment> Inlines(string text)
    {
        List<WhatsNewSegment> segments = [];
        int position = 0;
        foreach (Match match in LinkPattern().Matches(text))
        {
            if (match.Index > position)
            {
                segments.Add(new WhatsNewText(text[position..match.Index]));
            }

            string label = match.Groups[1].Value;
            string url = match.Groups[2].Value;
            segments.Add(IsWebAddress(url) ? new WhatsNewLink(label, url) : new WhatsNewText(label));
            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new WhatsNewText(text[position..]));
        }

        return segments;
    }

    private static bool IsWebAddress(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkPattern();
}
