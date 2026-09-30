using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace NetPrints.Catalog.Tests.Format;

/// <summary>CT-T16 (npcat part): <see cref="CatalogSchema"/> and the committed <c>schemas/npcat.v1.schema.json</c>.</summary>
public sealed class CatalogSchemaTests
{
    private static string SchemaPath() => Path.Combine(TestPaths.RepositoryRoot(), "schemas", "npcat.v1.schema.json");

    private static readonly Lazy<JsonSchema> Committed = new(() => JsonSchema.FromText(File.ReadAllText(SchemaPath())));

    private static bool IsValid(string json) =>
        Committed.Value.Evaluate(JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List }).IsValid;

    [Fact]
    public void GeneratedSchemaMatchesTheCommittedFile()
    {
        string generated = CatalogSchema.GenerateV1();
        string path = SchemaPath();
        if (TestPaths.UpdateSnapshots)
        {
            File.WriteAllText(path, generated);
        }

        Assert.True(File.Exists(path), $"Missing schema file {path}; regenerate with {TestPaths.UpdateSnapshotsVariable}=1");
        Assert.Equal(File.ReadAllText(path), generated);
    }

    private static JsonObject Generated() =>
        JsonNode.Parse(CatalogSchema.GenerateV1()) as JsonObject ?? throw new InvalidOperationException("The generated schema is not an object.");

    [Fact]
    public void DeclaresItsOwnUrlAsId()
    {
        var schema = Generated();
        Assert.Equal(CatalogDocument.SchemaUrl, schema["$id"]?.GetValue<string>());
    }

    [Fact]
    public void SchemaVersionIsPinnedToOne()
    {
        var schema = Generated();
        Assert.Equal(1, schema["properties"]?["schemaVersion"]?["const"]?.GetValue<int>());
    }

    [Fact]
    public void RequiresIdVersionAndAssemblies()
    {
        JsonArray required = Generated()["required"] as JsonArray ?? throw new InvalidOperationException("No required list.");
        Assert.Equal(["assemblies", "id", "version"], required.Select(node => node?.GetValue<string>()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheGoldenCatalogValidates()
    {
        Assert.True(IsValid(File.ReadAllText(TestPaths.GoldenPath(SampleCatalog.GoldenFileName))));
    }

    [Theory]
    [InlineData("{ \"schemaVersion\": 1, \"version\": \"1\", \"assemblies\": [ { \"name\": \"A\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"Upper Case\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"A\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": 2, \"id\": \"a\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"A\", \"version\": \"1\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"a\", \"version\": \"1\", \"assemblies\": [] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"a\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"A\", \"version\": \"1\" } ], \"types\": [ { \"id\": \"T:A\", \"name\": \"A\", \"kind\": \"module\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"a\", \"version\": \"1\", \"assemblies\": [ { \"name\": \"A\", \"version\": \"1\" } ], \"types\": [ { \"id\": \"T:A\", \"name\": \"A\", \"kind\": \"class\", \"summary\": null } ] }")]
    public void RejectsInvalidCatalogs(string json)
    {
        Assert.False(IsValid(json));
    }
}
