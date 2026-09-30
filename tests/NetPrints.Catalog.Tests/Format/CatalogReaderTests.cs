using System.IO;
using System.Text;
using Xunit;

namespace NetPrints.Catalog.Tests.Format;

/// <summary>CT-T03: reading catalog files (contracts/catalog.md §1, reader).</summary>
public sealed class CatalogReaderTests
{
    private const string Minimal = """
        { "schemaVersion": 1, "id": "lib", "version": "1.0.0.0",
          "assemblies": [ { "name": "Lib", "version": "1.0.0.0" } ] }
        """;

    [Fact]
    public void RoundTripIsTheIdentity()
    {
        string written = CanonicalCatalogWriter.Write(SampleCatalog.Build());

        CatalogDocument read = CatalogReader.Read(written);

        Assert.Equal(written, CanonicalCatalogWriter.Write(read));
    }

    [Fact]
    public void ReadsEveryFieldOfTheGolden()
    {
        CatalogDocument read = CatalogReader.Read(File.ReadAllText(TestPaths.GoldenPath(SampleCatalog.GoldenFileName)));

        Assert.Equal(1, read.SchemaVersion);
        Assert.Equal("samplelib", read.Id);
        Assert.Equal("public-api", read.Profile);
        Assert.Equal(2, read.Assemblies.Count);
        Assert.Equal(3, read.Types.Count);

        CatalogType vector = read.Types[1];
        Assert.Equal(CatalogTypeKind.Struct, vector.Kind);
        Assert.Equal(["sealed"], vector.Modifiers);
        Assert.True(vector.Interfaces?[1].IsInterface);

        CatalogMethod tryParse = Assert.IsType<CatalogMethod>(vector.Methods?[1]);
        Assert.Equal(CatalogVisibility.Protected, tryParse.Visibility);
        Assert.Equal(CatalogPassType.Out, tryParse.Parameters?[1].PassType);
        Assert.Equal("10", tryParse.Parameters?[2].Default?.Value);
        Assert.True(tryParse.Parameters?[3].Params);
        Assert.True(tryParse.Obsolete?.Error);
        Assert.True(tryParse.ReturnType?.Args?[0].Generic);
        Assert.Equal(CatalogVariableKind.Field, vector.Variables?[0].Kind);
        Assert.Equal(CatalogVisibility.Protected, vector.Variables?[1].Set);
    }

    [Fact]
    public void ReadsFromAStream()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(Minimal));

        CatalogDocument read = CatalogReader.Read(stream);

        Assert.Equal("lib", read.Id);
        Assert.Empty(read.Types);
    }

    [Fact]
    public void ToleratesUnknownPropertiesAndAnyWhitespace()
    {
        const string json = """
            {"unknown":{"a":[1,2]},"schemaVersion":1,"id":"lib","version":"1.0.0.0","assemblies":[{"name":"Lib","version":"1.0.0.0","extra":true}],
             "types":[{"id":"T:A","name":"A","kind":"class","future":"x","methods":[{"id":"M:A.B","name":"B","visibility":"public","parameters":[{"name":"p","type":{"name":"System.Int32","more":1},"other":null}]}]}]}
            """;

        CatalogDocument read = CatalogReader.Read(json);

        Assert.Equal("M:A.B", read.Types[0].Methods?[0].Id);
    }

    [Fact]
    public void RejectsANewerSchemaVersionWithNpc101()
    {
        CatalogFormatException error = Assert.Throws<CatalogFormatException>(() => CatalogReader.Read(Minimal.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", System.StringComparison.Ordinal)));

        Assert.Equal(CatalogDiagnosticCodes.UnsupportedSchemaVersion, error.Code);
        Assert.Contains("2", error.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": \"1\", \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\", \"version\": \"1\" } ], \"types\": [ { \"id\": \"T:A\", \"name\": \"A\", \"kind\": \"module\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"lib\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"Lib\", \"version\": \"1\" } ], \"types\": [ { \"id\": \"T:A\", \"name\": \"A\", \"kind\": \"class\" }, { \"id\": \"T:A\", \"name\": \"A\", \"kind\": \"class\" } ] }")]
    public void RejectsMalformedFilesWithNpc102(string json)
    {
        CatalogFormatException error = Assert.Throws<CatalogFormatException>(() => CatalogReader.Read(json));

        Assert.Equal(CatalogDiagnosticCodes.MalformedCatalog, error.Code);
    }
}
