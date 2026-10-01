using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>contracts/shell.md §2, data-model "Shell": the document id forms round-trip, malformed strings are rejected, equality is by value.</summary>
public class DocumentIdTests
{
    [Theory]
    [InlineData("graph:Classes/Player.npclass#method:m1")]
    [InlineData("graph:Classes/Player.npclass#ctor:c1")]
    [InlineData("graph:Classes/Player.npclass#event:e1")]
    [InlineData("graph:Classes/Player.npclass#class")]
    [InlineData("graph:Player.npclass#method:3f2a")]
    [InlineData("start")]
    [InlineData("project-settings")]
    public void ARoundTripThroughToStringAndTryParseKeepsTheValue(string text)
    {
        Assert.True(DocumentId.TryParse(text, out var id));

        Assert.Equal(text, id.ToString());
        Assert.True(DocumentId.TryParse(id.ToString(), out var again));
        Assert.Equal(id, again);
    }

    [Fact]
    public void AGraphIdExposesItsParts()
    {
        Assert.True(DocumentId.TryParse("graph:Classes/Player.npclass#method:m1", out var id));

        Assert.Equal(DocumentKind.Graph, id.Kind);
        Assert.Equal("Classes/Player.npclass", id.ClassPath);
        Assert.Equal("method:m1", id.GraphKey);
    }

    [Fact]
    public void TheFactoriesBuildTheSameValuesAsTryParse()
    {
        Assert.True(DocumentId.TryParse("graph:A.npclass#event:e1", out var parsed));

        Assert.Equal(parsed, DocumentId.Graph("A.npclass", "event:e1"));
        Assert.Equal("start", DocumentId.StartPage.ToString());
        Assert.Equal(DocumentKind.StartPage, DocumentId.StartPage.Kind);
        Assert.Equal("project-settings", DocumentId.ProjectSettings.ToString());
        Assert.Equal(DocumentKind.ProjectSettings, DocumentId.ProjectSettings.Kind);
        Assert.Null(DocumentId.StartPage.ClassPath);
        Assert.Null(DocumentId.StartPage.GraphKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("graph")]
    [InlineData("graph:")]
    [InlineData("graph:A.npclass")]
    [InlineData("graph:A.npclass#")]
    [InlineData("graph:#class")]
    [InlineData("graph:A.npclass#method:")]
    [InlineData("graph:A.npclass#unknown:1")]
    [InlineData("graph:A.npclass#classy")]
    [InlineData("start ")]
    [InlineData("Start")]
    [InlineData("project-settings:1")]
    [InlineData("other")]
    public void AMalformedStringIsRejected(string text)
    {
        Assert.False(DocumentId.TryParse(text, out _));
    }

    [Fact]
    public void ANullStringIsRejected() => Assert.False(DocumentId.TryParse(null, out _));

    [Fact]
    public void TheFactoryRejectsAnInvalidGraphKeyOrEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => DocumentId.Graph("", "class"));
        Assert.Throws<ArgumentException>(() => DocumentId.Graph("A.npclass", "bogus"));
    }

    [Fact]
    public void EqualValuesAreEqualAndDifferentOnesAreNot()
    {
        var a = DocumentId.Graph("A.npclass", "method:m1");
        var b = DocumentId.Graph("A.npclass", "method:m1");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, DocumentId.Graph("A.npclass", "method:m2"));
        Assert.NotEqual(a, DocumentId.Graph("B.npclass", "method:m1"));
        Assert.NotEqual<DocumentId>(DocumentId.StartPage, DocumentId.ProjectSettings);
        Assert.Equal(DocumentId.StartPage, DocumentId.StartPage);
    }
}
