using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace NetPrints.Catalog.Tests.Config;

/// <summary>CT-T10: parsing, relative paths, override and append rules, inline profile and invalid files of <c>netprints.catalog.json</c>.</summary>
public sealed class CatalogConfigTests
{
    private static readonly string ConfigDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "np-cfg", "project"));

    private static readonly string CurrentDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "np-cfg", "cwd"));

    private static string ExamplePath() => Path.Combine(TestPaths.RepositoryRoot(), "tests", "NetPrints.Catalog.Tests", "Config", "netprints.catalog.json");

    private static CatalogConfig Example() => CatalogConfigResolver.Read(ExamplePath());

    private static ResolvedCatalogConfig Resolve(CatalogConfig? file, CatalogOverrides? overrides = null) =>
        CatalogConfigResolver.Resolve(file, ConfigDirectory, overrides ?? new CatalogOverrides(), CurrentDirectory);

    [Fact]
    public void ParsesTheContractExample()
    {
        CatalogConfig config = Example();

        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal(CatalogConfig.SchemaUrl, config.Schema);
        Assert.NotNull(config.Sources);
        Assert.Equal("Newtonsoft.Json", config.Sources[0].Package);
        Assert.Equal("13.0.3", config.Sources[0].Version);
        Assert.Equal("libs/MyLib.dll", config.Sources[1].Assembly);
        Assert.Equal(["MyLib.Public.*"], config.Include ?? []);
        Assert.Equal(["*.Internal.*"], config.Exclude ?? []);
        Assert.Equal(JsonValueKind.String, config.Profile?.ValueKind);
        Assert.Equal(CatalogOutputFormat.Catalog, config.Output?.Format);
        Assert.Equal("catalogs/mylib.npcat.json", config.Output?.Path);
    }

    [Fact]
    public void ResolvesRelativePathsAgainstTheConfigDirectory()
    {
        ResolvedCatalogConfig resolved = Resolve(Example());

        Assert.Equal(Path.Combine(ConfigDirectory, "libs", "MyLib.dll"), resolved.Sources[1].Assembly);
        Assert.Equal("Newtonsoft.Json", resolved.Sources[0].Package);
        Assert.Equal(Path.Combine(ConfigDirectory, "catalogs", "mylib.npcat.json"), resolved.OutputPath);
        Assert.Equal(ConfigDirectory, resolved.BaseDirectory);
        Assert.Equal("public-api", resolved.ProfileReference);
        Assert.Equal(CatalogConfigResolver.DefaultTargetFramework, resolved.TargetFramework);
    }

    [Fact]
    public void CommandLineSourcesReplaceTheFilesAndUseTheCurrentDirectory()
    {
        CatalogOverrides overrides = new() { Sources = [new CatalogSourceConfig { Assembly = "bin/Other.dll" }] };

        ResolvedCatalogConfig resolved = Resolve(Example(), overrides);

        CatalogSourceConfig source = Assert.Single(resolved.Sources);
        Assert.Equal(Path.Combine(CurrentDirectory, "bin", "Other.dll"), source.Assembly);
    }

    [Fact]
    public void IncludeAndExcludeAreAppendedAndOtherListsReplaced()
    {
        CatalogConfig file = Example() with { ReferencePaths = ["ref-a"], Extensions = ["ext-a"] };
        CatalogOverrides overrides = new()
        {
            Include = ["Extra.*"],
            Exclude = ["*.Hidden"],
            ReferencePaths = ["ref-b"],
            Extensions = ["ext-b"],
        };

        ResolvedCatalogConfig resolved = Resolve(file, overrides);

        Assert.Equal(["MyLib.Public.*", "Extra.*"], resolved.Include);
        Assert.Equal(["*.Internal.*", "*.Hidden"], resolved.Exclude);
        Assert.Equal([Path.Combine(CurrentDirectory, "ref-b")], resolved.ReferencePaths);
        Assert.Equal([Path.Combine(CurrentDirectory, "ext-b")], resolved.Extensions);
    }

    [Fact]
    public void ScalarOverridesWinOverTheFile()
    {
        CatalogOverrides overrides = new()
        {
            Id = "other",
            Version = "2.0",
            Profile = "annotated",
            TargetFramework = "net9.0",
            Format = CatalogOutputFormat.Csharp,
            OutputPath = "out/Catalog.g.cs",
            ClassName = "MyCatalog",
            Namespace = "My.App",
        };

        ResolvedCatalogConfig resolved = Resolve(Example(), overrides);

        Assert.Equal("other", resolved.Id);
        Assert.Equal("2.0", resolved.Version);
        Assert.Equal("annotated", resolved.ProfileReference);
        Assert.Equal("net9.0", resolved.TargetFramework);
        Assert.Equal(CatalogOutputFormat.Csharp, resolved.Format);
        Assert.Equal(Path.Combine(CurrentDirectory, "out", "Catalog.g.cs"), resolved.OutputPath);
        Assert.Equal("MyCatalog", resolved.ClassName);
        Assert.Equal("My.App", resolved.Namespace);
    }

    [Fact]
    public void AProfileFilePathIsMadeAbsolute()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""{ "sources": [ { "assembly": "a.dll" } ], "profile": "profiles/mine.npprofile.json" }""");

        Assert.Equal(Path.Combine(ConfigDirectory, "profiles", "mine.npprofile.json"), Resolve(file).ProfileReference);
    }

    [Fact]
    public void AnInlineProfileIsKeptAsJsonAndValidated()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""
            { "sources": [ { "assembly": "a.dll" } ], "profile": { "schemaVersion": 1, "id": "inline", "base": "none", "includeNamespaces": [ "A.*" ] } }
            """);

        ResolvedCatalogConfig resolved = Resolve(file);

        Assert.Null(resolved.ProfileReference);
        Assert.NotNull(resolved.InlineProfileJson);
        Assert.Contains("\"inline\"", resolved.InlineProfileJson, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInlineProfileWithACommentAndATrailingCommaIsAccepted()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""
            { "sources": [ { "assembly": "a.dll" } ], "profile": { // why
                "schemaVersion": 1, "id": "inline", "base": "none", "includeNamespaces": [ "A.*", ], } }
            """);

        ResolvedCatalogConfig resolved = Resolve(file);

        Assert.NotNull(resolved.InlineProfileJson);
        Assert.DoesNotContain("//", resolved.InlineProfileJson, StringComparison.Ordinal);
        Assert.Equal("inline", ProfileJson.Parse(resolved.InlineProfileJson).Id);
    }

    [Fact]
    public void AnInvalidInlineProfileFails()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""{ "sources": [ { "assembly": "a.dll" } ], "profile": { "base": "everything" } }""");

        CatalogConfigException exception = Assert.Throws<CatalogConfigException>(() => Resolve(file));

        Assert.Contains("profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACommandLineProfileReplacesTheInlineOne()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""{ "sources": [ { "assembly": "a.dll" } ], "profile": { "id": "inline" } }""");

        ResolvedCatalogConfig resolved = Resolve(file, new CatalogOverrides { Profile = "annotated" });

        Assert.Equal("annotated", resolved.ProfileReference);
        Assert.Null(resolved.InlineProfileJson);
    }

    [Fact]
    public void WithoutAFileTheCommandLineAloneIsEnough()
    {
        ResolvedCatalogConfig resolved = CatalogConfigResolver.Resolve(
            null,
            null,
            new CatalogOverrides { Sources = [new CatalogSourceConfig { Package = "A", Version = "1.0.0" }] },
            CurrentDirectory);

        Assert.Equal(CurrentDirectory, resolved.BaseDirectory);
        Assert.Null(resolved.OutputPath);
        Assert.Equal(CatalogOutputFormat.Catalog, resolved.Format);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("""{ "sources": "a.dll" }""")]
    [InlineData("""{ "sources": [], "output": { "format": "xml" } }""")]
    public void AnInvalidFileFails(string json)
    {
        Assert.Throws<CatalogConfigException>(() => CatalogConfigResolver.Parse(json));
    }

    [Fact]
    public void ANewerSchemaVersionNamesBothVersions()
    {
        CatalogConfigException exception = Assert.Throws<CatalogConfigException>(() =>
            CatalogConfigResolver.Parse("""{ "schemaVersion": 2, "sources": [ { "assembly": "a.dll" } ] }"""));

        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentsAndTrailingCommasAreTolerated()
    {
        CatalogConfig config = CatalogConfigResolver.Parse("""
            {
              // where the catalog comes from
              "sources": [ { "assembly": "a.dll" }, ],
            }
            """);

        Assert.Single(config.Sources ?? []);
    }

    [Theory]
    [InlineData("""{ "assembly": "a.dll", "package": "P", "version": "1" }""")]
    [InlineData("""{ "package": "P" }""")]
    [InlineData("""{ "project": "p.csproj" }""")]
    [InlineData("""{ "assembly": "a.dll", "assemblies": [ "A" ] }""")]
    [InlineData("""{ }""")]
    public void AnInvalidSourceFails(string source)
    {
        CatalogConfig file = CatalogConfigResolver.Parse($$"""{ "sources": [ {{source}} ] }""");

        Assert.Throws<CatalogConfigException>(() => Resolve(file));
    }

    [Fact]
    public void NoSourcesAtAllFails()
    {
        Assert.Throws<CatalogConfigException>(() => Resolve(CatalogConfigResolver.Parse("{ }")));
        Assert.Throws<CatalogConfigException>(() => Resolve(null));
    }

    [Fact]
    public void AProjectSourceKeepsItsAssemblyNames()
    {
        CatalogConfig file = CatalogConfigResolver.Parse("""{ "sources": [ { "project": "src/App.csproj", "assemblies": [ "Lib" ] } ] }""");

        CatalogSourceConfig source = Assert.Single(Resolve(file).Sources);

        Assert.Equal(Path.Combine(ConfigDirectory, "src", "App.csproj"), source.Project);
        Assert.Equal(["Lib"], source.Assemblies ?? []);
    }
}
