using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>Profile files: <c>*.npprofile.json</c> (data-model.md section 2, contracts/catalog.md section 6).</summary>
public sealed class ProfileJsonTests
{
    private const string Full = """
        {
          "$schema": "https://danielmeza.github.io/netprints/schemas/npprofile.v1.schema.json",
          "schemaVersion": 1,
          "id": "my-profile",
          "base": "annotated",
          "includeNamespaces": ["Fixture.*"],
          "excludeNamespaces": ["Fixture.Legacy"],
          "includeTypes": ["Fixture.Utilities.Helpers", "Fixture.G?.*"],
          "excludeTypes": ["*.Secret"],
          "typeAttributes": [
            { "rule": "exclude", "attribute": "System.ObsoleteAttribute" }
          ],
          "memberAttributes": [
            { "rule": "require", "attribute": "Fixture.Attributes.ExposeAttribute", "argument": { "name": "Flags", "contains": "Callable" } },
            { "rule": "require", "attribute": "Fixture.Attributes.ExposeAttribute", "argument": { "position": 0, "equals": "Callable" } }
          ],
          "obsolete": "include",
          "unknownFutureField": { "ignored": true }
        }
        """;

    [Fact]
    public void ReadsEveryField()
    {
        CatalogProfile profile = ProfileJson.Parse(Full);

        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("my-profile", profile.Id);
        Assert.Equal(CatalogProfileBase.Annotated, profile.Base);
        Assert.Equal(["Fixture.*"], profile.IncludeNamespaces);
        Assert.Equal(["Fixture.Legacy"], profile.ExcludeNamespaces);
        Assert.Equal(["Fixture.Utilities.Helpers", "Fixture.G?.*"], profile.IncludeTypes);
        Assert.Equal(["*.Secret"], profile.ExcludeTypes);
        Assert.Equal(CatalogObsoleteMode.Include, profile.Obsolete);

        CatalogAttributeRule typeRule = Assert.Single(profile.TypeAttributes ?? []);
        Assert.Equal(CatalogAttributeRuleKind.Exclude, typeRule.Rule);
        Assert.Equal("System.ObsoleteAttribute", typeRule.Attribute);
        Assert.Null(typeRule.Argument);

        Assert.NotNull(profile.MemberAttributes);
        Assert.Equal(2, profile.MemberAttributes.Count);
        CatalogArgumentMatch byName = Assert.IsType<CatalogArgumentMatch>(profile.MemberAttributes[0].Argument);
        Assert.Equal("Flags", byName.Name);
        Assert.Null(byName.Position);
        Assert.Equal("Callable", byName.ContainsValue);
        Assert.Null(byName.EqualsValue);
        CatalogArgumentMatch byPosition = Assert.IsType<CatalogArgumentMatch>(profile.MemberAttributes[1].Argument);
        Assert.Equal(0, byPosition.Position);
        Assert.Equal("Callable", byPosition.EqualsValue);
    }

    [Fact]
    public void AppliesDefaults()
    {
        CatalogProfile profile = ProfileJson.Parse("""{ "id": "small" }""");

        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal(CatalogProfileBase.PublicApi, profile.Base);
        Assert.Equal(CatalogObsoleteMode.ExcludeErrors, profile.Obsolete);
        Assert.Null(profile.IncludeNamespaces);
        Assert.Null(profile.TypeAttributes);
    }

    [Fact]
    public void ReadsTheContractExample()
    {
        CatalogProfile profile = ProfileJson.Parse("""
            {
              "schemaVersion": 1,
              "id": "fixture-flags",
              "base": "none",
              "memberAttributes": [
                { "rule": "require", "attribute": "Fixture.Attributes.ExposeAttribute", "argument": { "name": "Flags", "contains": "Callable" } }
              ]
            }
            """);

        Assert.Equal(CatalogProfileBase.None, profile.Base);
        Assert.Equal("Flags", profile.MemberAttributes?.Single().Argument?.Name);
    }

    [Fact]
    public void ReadsTheFixtureProfileFile() =>
        Assert.Equal("fixture-flags", ProfileJson.Parse(System.IO.File.ReadAllText(FixtureLibrary.ProfilePath("fixture-flags.npprofile.json"))).Id);

    [Fact]
    public void DecodesEscapesAndUnicode()
    {
        CatalogProfile profile = ProfileJson.Parse("""{ "id": "x", "includeTypes": ["A\u0042\n\"\/\t"] }""");

        Assert.Equal(["AB\n\"/\t"], profile.IncludeTypes);
    }

    [Fact]
    public void NewerSchemaVersionIsNpc003NamingBothVersions()
    {
        CatalogFormatException failure = Assert.Throws<CatalogFormatException>(() => ProfileJson.Parse("""{ "schemaVersion": 2, "id": "x" }"""));

        Assert.Equal(CatalogDiagnosticCodes.InvalidProfileFile, failure.Code);
        Assert.Contains("2", failure.Message, System.StringComparison.Ordinal);
        Assert.Contains("1", failure.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("""{ "id": "x", }""")]
    [InlineData("""{ "id": "x" } trailing""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "id": "" }""")]
    [InlineData("""{ "id": "Upper" }""")]
    [InlineData("""{ "id": "public-api" }""")]
    [InlineData("""{ "id": "annotated" }""")]
    [InlineData("""{ "id": 5 }""")]
    [InlineData("""{ "schemaVersion": "1", "id": "x" }""")]
    [InlineData("""{ "schemaVersion": 0, "id": "x" }""")]
    [InlineData("""{ "id": "x", "base": "everything" }""")]
    [InlineData("""{ "id": "x", "obsolete": "maybe" }""")]
    [InlineData("""{ "id": "x", "includeNamespaces": "A" }""")]
    [InlineData("""{ "id": "x", "includeNamespaces": [1] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "attribute": "A" } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "maybe", "attribute": "A" } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require" } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require", "attribute": "A", "argument": { "contains": "v" } } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require", "attribute": "A", "argument": { "name": "N", "position": 0, "contains": "v" } } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require", "attribute": "A", "argument": { "name": "N" } } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require", "attribute": "A", "argument": { "name": "N", "equals": "a", "contains": "b" } } ] }""")]
    [InlineData("""{ "id": "x", "typeAttributes": [ { "rule": "require", "attribute": "A", "argument": { "position": -1, "equals": "a" } } ] }""")]
    public void MalformedOrInvalidProfilesAreNpc003(string json)
    {
        CatalogFormatException failure = Assert.Throws<CatalogFormatException>(() => ProfileJson.Parse(json));

        Assert.Equal(CatalogDiagnosticCodes.InvalidProfileFile, failure.Code);
    }
}
