using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T06: the <c>fixture-flags</c> profile file (argument <c>contains</c> on enum flags) and the argument matching rules.</summary>
public sealed class CustomProfileTests
{
    private static string[] MethodNames(CatalogBuildResult result) =>
        [.. result.Document.Types.SelectMany(type => (type.Methods ?? []).Select(method => $"{type.Name}.{method.Name}"))];

    [Fact]
    public void EqualsTheCommittedSnapshot()
    {
        CatalogBuildResult result = FixtureCatalog.BuildFixture(FixtureCatalog.FlagsProfile(), FixtureCatalog.FlagsId);

        FixtureCatalog.AssertSnapshot("fixture-flags.npcat.json", result);
        Assert.Equal("fixture-flags", result.Document.Profile);
        Assert.Equal(FixtureCatalog.FlagsId, result.Document.Id);
    }

    [Fact]
    public void KeepsOnlyMembersWhoseFlagsContainCallable()
    {
        CatalogBuildResult result = FixtureCatalog.BuildFixture(FixtureCatalog.FlagsProfile(), FixtureCatalog.FlagsId);

        Assert.Equal(["Counter.Step", "Helpers.Double", "Helpers.TryParse"], MethodNames(result));
        Assert.All(result.Document.Types, type => Assert.Null(type.Variables));
    }

    [Fact]
    public void EqualsByPositionMatchesTheExactRenderedValue()
    {
        CatalogProfile profile = new("by-position", CatalogProfileBase.None, MemberAttributes:
        [
            new CatalogAttributeRule(CatalogAttributeRuleKind.Require, "Fixture.Attributes.ExposeAttribute", new CatalogArgumentMatch(Position: 0, EqualsValue: "Callable")),
        ]);

        CatalogBuildResult result = FixtureCatalog.BuildFixture(profile, "by-position");

        Assert.Equal(["Counter.Step", "Helpers.Double"], MethodNames(result));
    }

    [Theory]
    [InlineData("Flags", "Readable", true)]
    [InlineData("flags", "Readable", true)]
    [InlineData("Flags", "Read", false)]
    [InlineData("Flags", "Missing", false)]
    [InlineData("Other", "Readable", false)]
    public void NameMatchesTheConstructorParameterAndItsProperty(string name, string contains, bool matchesCount)
    {
        CatalogProfile profile = new("by-name", CatalogProfileBase.None, MemberAttributes:
        [
            new CatalogAttributeRule(CatalogAttributeRuleKind.Require, "Fixture.Attributes.ExposeAttribute", new CatalogArgumentMatch(Name: name, ContainsValue: contains)),
        ]);

        CatalogBuildResult result = FixtureCatalog.BuildFixture(profile, "by-name");
        CatalogType? counter = result.Document.Types.SingleOrDefault(type => type.Id == "T:Fixture.Utilities.Counter");

        Assert.Equal(matchesCount, counter?.Variables?.Any(v => v.Name == "Count") ?? false);
    }

    [Fact]
    public void AnAttributeRuleWithoutAnArgumentMatchesEveryUse()
    {
        CatalogProfile profile = new("any-expose", CatalogProfileBase.None, MemberAttributes:
        [
            new CatalogAttributeRule(CatalogAttributeRuleKind.Require, "Fixture.Attributes.ExposeAttribute"),
        ]);

        CatalogBuildResult result = FixtureCatalog.BuildFixture(profile, "any-expose");

        Assert.Equal(["Counter.Step", "Helpers.Double", "Helpers.TryParse"], MethodNames(result));
        Assert.Contains(FixtureCatalog.TypeOf(result, "T:Fixture.Utilities.Counter").Variables ?? [], v => v.Name == "Count");
    }
}
