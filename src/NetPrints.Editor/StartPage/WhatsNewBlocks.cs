namespace NetPrints.Editor.StartPage;

/// <summary>A run of text inside a block of the release notes.</summary>
internal abstract record WhatsNewSegment;

/// <summary>Plain text.</summary>
/// <param name="Text">The text.</param>
internal sealed record WhatsNewText(string Text) : WhatsNewSegment;

/// <summary>A link to a web page.</summary>
/// <param name="Text">The link text.</param>
/// <param name="Url">The absolute http or https address.</param>
internal sealed record WhatsNewLink(string Text, string Url) : WhatsNewSegment;

/// <summary>A block of the release notes.</summary>
/// <param name="Segments">The block's text and links.</param>
internal abstract record WhatsNewBlock(IReadOnlyList<WhatsNewSegment> Segments)
{
    /// <summary>Gets the block's text without its links' addresses.</summary>
    public string PlainText => string.Concat(Segments.Select(segment => segment switch
    {
        WhatsNewLink link => link.Text,
        WhatsNewText text => text.Text,
        _ => "",
    }));
}

/// <summary>A heading.</summary>
/// <param name="Level">1 to 6.</param>
/// <param name="Segments">The heading's text.</param>
internal sealed record WhatsNewHeading(int Level, IReadOnlyList<WhatsNewSegment> Segments) : WhatsNewBlock(Segments);

/// <summary>A bullet.</summary>
/// <param name="Segments">The bullet's text and links.</param>
internal sealed record WhatsNewBullet(IReadOnlyList<WhatsNewSegment> Segments) : WhatsNewBlock(Segments);

/// <summary>A paragraph.</summary>
/// <param name="Segments">The paragraph's text and links.</param>
internal sealed record WhatsNewParagraph(IReadOnlyList<WhatsNewSegment> Segments) : WhatsNewBlock(Segments);
