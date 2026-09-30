using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace NetPrints.Catalog;

/// <summary>Turns the XML of a <c>&lt;summary&gt;</c>, <c>&lt;param&gt;</c> or <c>&lt;returns&gt;</c> element into plain text (data-model.md §6).</summary>
internal static class SummaryNormalizer
{
    private const string ParagraphSeparator = "\n\n";

    private const int CrefPrefixLength = 2;

    private const string ArityMarker = "`";

    /// <summary>Normalizes the content of <paramref name="element"/>.</summary>
    /// <param name="element">The element, or <see langword="null"/>.</param>
    /// <returns>The text, or <see langword="null"/> when it is empty.</returns>
    public static string? Normalize(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        List<string> paragraphs = [];
        StringBuilder current = new();
        Walk(element.Nodes(), paragraphs, current);
        Flush(paragraphs, current);
        return paragraphs.Count == 0 ? null : string.Join(ParagraphSeparator, paragraphs);
    }

    private static void Walk(IEnumerable<XNode> nodes, List<string> paragraphs, StringBuilder current)
    {
        foreach (XNode node in nodes)
        {
            switch (node)
            {
                case XText text:
                    current.Append(text.Value);
                    break;
                case XElement element:
                    WalkElement(element, paragraphs, current);
                    break;
            }
        }
    }

    private static void WalkElement(XElement element, List<string> paragraphs, StringBuilder current)
    {
        switch (element.Name.LocalName)
        {
            case "para":
                Flush(paragraphs, current);
                Walk(element.Nodes(), paragraphs, current);
                Flush(paragraphs, current);
                break;
            case "see":
            case "seealso":
                AppendReference(element, paragraphs, current);
                break;
            case "paramref":
            case "typeparamref":
                current.Append((string?)element.Attribute("name"));
                break;
            case "inheritdoc":
                break;
            default:
                Walk(element.Nodes(), paragraphs, current);
                break;
        }
    }

    private static void AppendReference(XElement element, List<string> paragraphs, StringBuilder current)
    {
        if ((string?)element.Attribute("cref") is { } cref)
        {
            current.Append(CrefName(cref));
        }
        else if ((string?)element.Attribute("langword") is { } keyword)
        {
            current.Append(keyword);
        }
        else if (element.FirstNode is not null)
        {
            Walk(element.Nodes(), paragraphs, current);
        }
        else if ((string?)element.Attribute("href") is { } href)
        {
            current.Append(href);
        }
    }

    private static string CrefName(string cref)
    {
        string name = cref.Length > 1 && cref[1] == ':' ? cref.Substring(CrefPrefixLength) : cref;
        int parenthesis = name.IndexOf("(", System.StringComparison.Ordinal);
        if (parenthesis >= 0)
        {
            name = name.Substring(0, parenthesis);
        }

        string[] segments = name.Split('.');
        string last = segments[segments.Length - 1];
        if (last == "#ctor" && segments.Length > 1)
        {
            last = segments[segments.Length - CrefPrefixLength];
        }

        int arity = last.IndexOf(ArityMarker, System.StringComparison.Ordinal);
        return arity >= 0 ? last.Substring(0, arity) : last;
    }

    private static void Flush(List<string> paragraphs, StringBuilder current)
    {
        StringBuilder collapsed = new(current.Length);
        bool pendingSpace = false;
        for (int index = 0; index < current.Length; index++)
        {
            char character = current[index];
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                collapsed.Append(' ');
                pendingSpace = false;
            }

            collapsed.Append(character);
        }

        current.Clear();
        if (collapsed.Length > 0)
        {
            paragraphs.Add(collapsed.ToString());
        }
    }
}
