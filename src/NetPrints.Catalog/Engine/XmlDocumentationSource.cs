using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NetPrints.Catalog;

/// <summary>
/// An <see cref="IDocumentationSource"/> over compiler-produced XML documentation files, given as text (the caller
/// reads the files: file access is not available to the source generator). The first file that documents a symbol
/// wins; text that is not well-formed XML is skipped.
/// </summary>
public sealed class XmlDocumentationSource : IDocumentationSource
{
    private readonly Dictionary<string, XElement> members = new(System.StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="XmlDocumentationSource"/> class.</summary>
    /// <param name="xmlTexts">The texts of the documentation files, in priority order.</param>
    public XmlDocumentationSource(IEnumerable<string> xmlTexts)
    {
        Guard.NotNull(xmlTexts, nameof(xmlTexts));
        foreach (string text in xmlTexts)
        {
            Add(text);
        }
    }

    /// <summary>Gets a source that documents nothing.</summary>
    public static XmlDocumentationSource Empty { get; } = new([]);

    /// <summary>Creates a source from the text of one documentation file.</summary>
    /// <param name="xml">The file text.</param>
    /// <returns>The source.</returns>
    public static XmlDocumentationSource FromText(string xml) => new([xml]);

    /// <inheritdoc />
    public string? GetSummary(string documentationId) =>
        members.TryGetValue(documentationId, out XElement? member) ? SummaryNormalizer.Normalize(member.Element("summary")) : null;

    /// <inheritdoc />
    public string? GetParameter(string documentationId, string parameterName) =>
        members.TryGetValue(documentationId, out XElement? member)
            ? SummaryNormalizer.Normalize(member.Elements("param").FirstOrDefault(p => (string?)p.Attribute("name") == parameterName))
            : null;

    /// <inheritdoc />
    public string? GetReturns(string documentationId) =>
        members.TryGetValue(documentationId, out XElement? member) ? SummaryNormalizer.Normalize(member.Element("returns")) : null;

    private static XDocument? Parse(string text)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using StringReader reader = new(text);
            using XmlReader xml = XmlReader.Create(reader, settings);
            return XDocument.Load(xml);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private void Add(string text)
    {
        if (Parse(text)?.Root?.Element("members") is not { } container)
        {
            return;
        }

        foreach (XElement member in container.Elements("member"))
        {
            if ((string?)member.Attribute("name") is { } name && !members.ContainsKey(name))
            {
                members.Add(name, member);
            }
        }
    }
}
