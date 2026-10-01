using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T07: the glob dialect of profiles (<c>*</c> and <c>?</c> over ordinal names).</summary>
public sealed class GlobTests
{
    [Theory]
    [InlineData("Fixture.Geometry", "Fixture.Geometry", true)]
    [InlineData("Fixture.Geometry", "Fixture.geometry", false)]
    [InlineData("Fixture.*", "Fixture.Geometry", true)]
    [InlineData("Fixture.*", "Fixture.Geometry.Deep", true)]
    [InlineData("Fixture.*", "Fixture", false)]
    [InlineData("*", "Anything.At.All", true)]
    [InlineData("*", "", true)]
    [InlineData("*Helpers", "Fixture.Utilities.Helpers", true)]
    [InlineData("*.Helpers", "Helpers", false)]
    [InlineData("Fixture.Generics.Box`?", "Fixture.Generics.Box`1", true)]
    [InlineData("Fixture.Generics.Box`?", "Fixture.Generics.Box`12", false)]
    [InlineData("Fixture.Generics.Box?1", "Fixture.Generics.Box`1", true)]
    [InlineData("Fixture.Generics.Box`1.Handle", "Fixture.Generics.Box`1.Handle", true)]
    [InlineData("Fixture.Generics.Box`1.*", "Fixture.Generics.Box`1.Handle", true)]
    [InlineData("A.B", "AxB", false)]
    [InlineData("A.?", "A.", false)]
    [InlineData("A.?", "A.b", true)]
    [InlineData("*a*b*", "xxaxxbxx", true)]
    [InlineData("*a*b*", "xxbxxaxx", false)]
    [InlineData("a*a*a", "aaa", true)]
    [InlineData("a*a*a", "aa", false)]
    [InlineData("", "", true)]
    [InlineData("", "x", false)]
    [InlineData("[a]", "[a]", true)]
    [InlineData("a+b", "a+b", true)]
    public void MatchesLiteralDotsWildcardsAndOrdinalCase(string pattern, string text, bool expected) =>
        Assert.Equal(expected, Glob.IsMatch(pattern, text));

    [Fact]
    public void AnyMatchIsFalseForNoPatternsAndTrueWhenOneMatches()
    {
        Assert.False(Glob.AnyMatch(null, "A.B"));
        Assert.False(Glob.AnyMatch([], "A.B"));
        Assert.False(Glob.AnyMatch(["C.*", "D"], "A.B"));
        Assert.True(Glob.AnyMatch(["C.*", "A.*"], "A.B"));
    }
}
