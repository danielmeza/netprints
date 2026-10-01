using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>Summary normalization (data-model.md §6) through <see cref="XmlDocumentationSource"/>.</summary>
public sealed class XmlDocumentationSourceTests
{
    private static IDocumentationSource Source(string members) =>
        XmlDocumentationSource.FromText($"<?xml version=\"1.0\"?><doc><assembly><name>A</name></assembly><members>{members}</members></doc>");

    private static string? Summary(string inner) => Source($"<member name=\"T:A.B\"><summary>{inner}</summary></member>").GetSummary("T:A.B");

    [Theory]
    [InlineData("Plain text.", "Plain text.")]
    [InlineData("  Spaces\n            and\tlines   collapse.  ", "Spaces and lines collapse.")]
    [InlineData("See <see cref=\"T:Fixture.Geometry.ShapeBase\"/> now.", "See ShapeBase now.")]
    [InlineData("See <see cref=\"T:System.Collections.Generic.List`1\"/> now.", "See List now.")]
    [InlineData("See <see cref=\"M:Fixture.Box`1.Map``1(System.Func{`0,``0})\"/> now.", "See Map now.")]
    [InlineData("See <seealso cref=\"P:Fixture.Box`1.Value\"/> now.", "See Value now.")]
    [InlineData("Is <see langword=\"null\"/> ok.", "Is null ok.")]
    [InlineData("Takes <paramref name=\"value\"/> of <typeparamref name=\"T\"/>.", "Takes value of T.")]
    [InlineData("Use <c>x + y</c> or <code>a  b</code>.", "Use x + y or a b.")]
    [InlineData("<para>First.</para><para>Second\n line.</para>", "First.\n\nSecond line.")]
    [InlineData("Lead <para>Middle.</para> tail.", "Lead\n\nMiddle.\n\ntail.")]
    [InlineData("<list><item>One</item><item>Two</item></list>", "OneTwo")]
    [InlineData("A &amp; B &lt; C", "A & B < C")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData("<inheritdoc />", null)]
    public void NormalizesTheSummaryText(string inner, string? expected) => Assert.Equal(expected, Summary(inner));

    [Fact]
    public void ReadsParameterAndReturnsText()
    {
        IDocumentationSource source = Source("<member name=\"M:A.B.M(System.Int32)\"><summary>Does it.</summary><param name=\"x\">The <c>x</c>.</param><param name=\"y\"> </param><returns>The result.</returns></member>");

        Assert.Equal("The x.", source.GetParameter("M:A.B.M(System.Int32)", "x"));
        Assert.Null(source.GetParameter("M:A.B.M(System.Int32)", "y"));
        Assert.Null(source.GetParameter("M:A.B.M(System.Int32)", "missing"));
        Assert.Equal("The result.", source.GetReturns("M:A.B.M(System.Int32)"));
    }

    [Fact]
    public void UnknownIdsAndMissingElementsAreNull()
    {
        IDocumentationSource source = Source("<member name=\"T:A.B\"><remarks>Only remarks.</remarks></member>");

        Assert.Null(source.GetSummary("T:A.B"));
        Assert.Null(source.GetSummary("T:A.Other"));
        Assert.Null(source.GetReturns("T:A.B"));
    }

    [Fact]
    public void MergesSeveralFilesFirstOneWinsAndSkipsMalformedText()
    {
        IDocumentationSource source = new XmlDocumentationSource(
        [
            "<doc><members><member name=\"T:A.B\"><summary>First.</summary></member></members></doc>",
            "this is not xml",
            "<doc><members><member name=\"T:A.B\"><summary>Second.</summary></member><member name=\"T:A.C\"><summary>Other.</summary></member></members></doc>",
        ]);

        Assert.Equal("First.", source.GetSummary("T:A.B"));
        Assert.Equal("Other.", source.GetSummary("T:A.C"));
    }

    [Fact]
    public void TheEmptySourceKnowsNothing()
    {
        Assert.Null(XmlDocumentationSource.Empty.GetSummary("T:A.B"));
        Assert.Null(XmlDocumentationSource.Empty.GetParameter("M:A.B.M", "x"));
        Assert.Null(XmlDocumentationSource.Empty.GetReturns("M:A.B.M"));
    }

    [Fact]
    public void DoctypeDeclarationsAreRejectedNotResolved()
    {
        IDocumentationSource source = XmlDocumentationSource.FromText("<!DOCTYPE doc [<!ENTITY x \"boom\">]><doc><members><member name=\"T:A.B\"><summary>&x;</summary></member></members></doc>");

        Assert.Null(source.GetSummary("T:A.B"));
    }
}
