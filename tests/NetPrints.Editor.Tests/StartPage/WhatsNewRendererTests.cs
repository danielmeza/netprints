using NetPrints.Editor.StartPage;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>What's new (FR-044): the Markdown subset the release notes use, rendered without a Markdown library.</summary>
public sealed class WhatsNewRendererTests
{
    private static string Text(IReadOnlyList<WhatsNewSegment> segments) =>
        string.Concat(segments.Select(segment => segment switch
        {
            WhatsNewLink link => $"[{link.Text}]({link.Url})",
            WhatsNewText text => text.Text,
            _ => throw new InvalidOperationException("Unknown segment."),
        }));

    private static string Describe(WhatsNewBlock block) => block switch
    {
        WhatsNewHeading heading => $"h{heading.Level}:{Text(heading.Segments)}",
        WhatsNewBullet bullet => $"li:{Text(bullet.Segments)}",
        WhatsNewParagraph paragraph => $"p:{Text(paragraph.Segments)}",
        _ => throw new InvalidOperationException("Unknown block."),
    };

    private static string[] Render(string markdown) => [.. WhatsNewRenderer.Parse(markdown).Select(Describe)];

    [Fact]
    public void HeadingsKeepTheirLevel() =>
        Assert.Equal(["h1:Title", "h2:Section", "h6:Deep"], Render("# Title\n## Section\n###### Deep"));

    [Fact]
    public void BulletsStartWithADashOrAnAsterisk() =>
        Assert.Equal(["li:one", "li:two"], Render("- one\n* two"));

    [Fact]
    public void LinesOfOneParagraphJoinAndABlankLineEndsIt() =>
        Assert.Equal(["p:first line second line", "p:next paragraph"], Render("first line\nsecond line\n\nnext paragraph"));

    [Fact]
    public void ALinkBecomesALinkSegmentBetweenTheTextAroundIt()
    {
        WhatsNewBullet bullet = Assert.IsType<WhatsNewBullet>(Assert.Single(WhatsNewRenderer.Parse("- See [the notes](https://example.com/x?a=1) now")));

        Assert.Collection(
            bullet.Segments,
            segment => Assert.Equal(new WhatsNewText("See "), segment),
            segment => Assert.Equal(new WhatsNewLink("the notes", "https://example.com/x?a=1"), segment),
            segment => Assert.Equal(new WhatsNewText(" now"), segment));
    }

    [Theory]
    [InlineData("[x](javascript:void)")]
    [InlineData("[x](docs/page.md)")]
    [InlineData("[x](file:///etc/passwd)")]
    public void OnlyHttpAndHttpsLinksAreLinks(string markdown) =>
        Assert.Equal(["p:x"], Render(markdown));

    [Fact]
    public void AHashWithoutASpaceIsNotAHeading() =>
        Assert.Equal(["p:#NotAHeading"], Render("#NotAHeading"));

    [Fact]
    public void WindowsLineEndingsAreAccepted() =>
        Assert.Equal(["h1:A", "li:b"], Render("# A\r\n- b\r\n"));

    [Theory]
    [InlineData("")]
    [InlineData("  \n\n ")]
    public void NothingToShowGivesNoBlocks(string markdown) =>
        Assert.Empty(WhatsNewRenderer.Parse(markdown));

    [Fact]
    public void TheBundledNotesHaveAHeadingBulletsAndALinkToTheReleasesPage()
    {
        IReadOnlyList<WhatsNewBlock> blocks = WhatsNewRenderer.Parse(WhatsNewResource.Read());

        Assert.IsType<WhatsNewHeading>(blocks[0]);
        Assert.Contains(blocks, block => block is WhatsNewBullet);
        Assert.Contains(blocks.SelectMany(block => block.Segments), segment => segment is WhatsNewLink { Url: WhatsNewTileViewModel.ReleasesUrl });
    }
}
