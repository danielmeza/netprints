using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>The evaluation order of a profile (data-model.md §2): base, globs, attribute rules, obsolete rule, <c>[NetPrintsIgnore]</c>.</summary>
public sealed class CatalogProfileFilterTests
{
    private const string ExposeAttribute = "Fixture.Attributes.ExposeAttribute";

    private const string NodeAttribute = "NetPrints.Annotations.NetPrintsNodeAttribute";

    private const string TypeAttribute = "NetPrints.Annotations.NetPrintsTypeAttribute";

    private static string[] TypeIds(CatalogProfile profile) =>
        [.. FixtureCatalog.BuildFixture(profile, "t").Document.Types.Select(type => type.Id)];

    private static string[] MethodNames(CatalogProfile profile, string typeId) =>
        [.. FixtureCatalog.BuildFixture(profile, "t").Document.Types.SingleOrDefault(type => type.Id == typeId)?.Methods?.Select(m => m.Name) ?? []];

    private static CatalogProfile Profile(
        IReadOnlyList<string>? includeNamespaces = null,
        IReadOnlyList<string>? excludeNamespaces = null,
        IReadOnlyList<string>? includeTypes = null,
        IReadOnlyList<string>? excludeTypes = null,
        IReadOnlyList<CatalogAttributeRule>? typeAttributes = null,
        IReadOnlyList<CatalogAttributeRule>? memberAttributes = null,
        CatalogObsoleteMode obsolete = CatalogObsoleteMode.ExcludeErrors) =>
        new("test", CatalogProfileBase.PublicApi, includeNamespaces, excludeNamespaces, includeTypes, excludeTypes, typeAttributes, memberAttributes, obsolete);

    [Fact]
    public void IncludeAndExcludeNamespacesNarrowTheBase()
    {
        Assert.All(TypeIds(Profile(includeNamespaces: ["Fixture.Geometry"])), id => Assert.StartsWith("T:Fixture.Geometry.", id, System.StringComparison.Ordinal));
        Assert.DoesNotContain(TypeIds(Profile(excludeNamespaces: ["Fixture.Legacy", "Fixture.Attributes"])), id => id.StartsWith("T:Fixture.Legacy.", System.StringComparison.Ordinal) || id.StartsWith("T:Fixture.Attributes.", System.StringComparison.Ordinal));
        Assert.Equal(["T:Fixture.Geometry.Circle"], TypeIds(Profile(includeNamespaces: ["Fixture.*"], includeTypes: ["*.Circle"])));
    }

    [Fact]
    public void TypeGlobsMatchTheFullNameWithArityAndDottedNesting()
    {
        Assert.Equal(["T:Fixture.Generics.Box`1", "T:Fixture.Generics.Box`1.Handle"], TypeIds(Profile(includeTypes: ["Fixture.Generics.Box`?", "Fixture.Generics.Box`1.*"])));
        Assert.DoesNotContain("T:Fixture.Utilities.Helpers", TypeIds(Profile(excludeTypes: ["*.Helpers"])));
    }

    [Fact]
    public void ObsoleteModes()
    {
        CatalogProfile Mode(CatalogObsoleteMode mode) => Profile(includeTypes: ["Fixture.Legacy.*"], obsolete: mode);

        Assert.Equal(["NewName", "OldName", "Removed"], MethodNames(Mode(CatalogObsoleteMode.Include), "T:Fixture.Legacy.Old").OrderBy(n => n, System.StringComparer.Ordinal));
        Assert.Equal(["NewName", "OldName"], MethodNames(Mode(CatalogObsoleteMode.ExcludeErrors), "T:Fixture.Legacy.Old").OrderBy(n => n, System.StringComparer.Ordinal));
        Assert.Equal(["NewName"], MethodNames(Mode(CatalogObsoleteMode.Exclude), "T:Fixture.Legacy.Old"));
        Assert.DoesNotContain("T:Fixture.Legacy.Ancient", TypeIds(Mode(CatalogObsoleteMode.Exclude)));
        Assert.Contains("T:Fixture.Legacy.Ancient", TypeIds(Mode(CatalogObsoleteMode.ExcludeErrors)));
    }

    [Fact]
    public void ARequireRuleNarrowsAPublicApiProfile()
    {
        CatalogProfile profile = Profile(typeAttributes: [new CatalogAttributeRule(CatalogAttributeRuleKind.Require, TypeAttribute)]);

        Assert.Equal(["T:Fixture.Utilities.Counter"], TypeIds(profile));
    }

    [Fact]
    public void AnExcludeRuleDropsMatchingMembers()
    {
        CatalogProfile profile = Profile(includeTypes: ["Fixture.Utilities.Helpers"], memberAttributes: [new CatalogAttributeRule(CatalogAttributeRuleKind.Exclude, NodeAttribute)]);

        Assert.DoesNotContain("Double", MethodNames(profile, "T:Fixture.Utilities.Helpers"));
        Assert.Contains("TryParse", MethodNames(profile, "T:Fixture.Utilities.Helpers"));
    }

    [Fact]
    public void ARuleAnAttributeArgumentMustEqualIsCaseSensitiveOnTheRenderedValue()
    {
        CatalogProfile Rule(string value) => new("eq", CatalogProfileBase.None, MemberAttributes:
        [
            new CatalogAttributeRule(CatalogAttributeRuleKind.Require, ExposeAttribute, new CatalogArgumentMatch(Name: "Flags", EqualsValue: value)),
        ]);

        Assert.Equal(["TryParse"], MethodNames(Rule("Callable, Readable"), "T:Fixture.Utilities.Helpers"));
        Assert.Empty(MethodNames(Rule("callable, readable"), "T:Fixture.Utilities.Helpers"));
    }

    [Fact]
    public void NetPrintsIgnoreExcludesEvenWhenARuleSelectsTheMember()
    {
        CatalogProfile profile = new("ignore", CatalogProfileBase.None, TypeAttributes: [new CatalogAttributeRule(CatalogAttributeRuleKind.Require, TypeAttribute)]);

        CatalogType counter = FixtureCatalog.TypeOf(FixtureCatalog.BuildFixture(profile, "ignore"), "T:Fixture.Utilities.Counter");

        Assert.DoesNotContain(counter.Methods ?? [], m => m.Name == "Reset");
        Assert.Contains(counter.Methods ?? [], m => m.Name == "Step");
    }

    [Fact]
    public void ANoneProfileWithoutRulesIsEmptyAndTheBuildStillSucceeds()
    {
        CatalogBuildResult result = FixtureCatalog.BuildFixture(new CatalogProfile("nothing", CatalogProfileBase.None), "nothing");

        Assert.Empty(result.Document.Types);
        Assert.Equal("CatalogFixtureLib", Assert.Single(result.Document.Assemblies).Name);
    }
}
