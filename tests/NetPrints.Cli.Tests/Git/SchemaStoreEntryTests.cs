using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>GI-T11: the SchemaStore catalog entries point at the committed schemas' <c>$id</c> and match the files git-install targets.</summary>
public sealed class SchemaStoreEntryTests
{
    private static readonly string Root = LocalSdkLayout.FindRepositoryRoot();

    private static JsonElement[] Entries()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "eng", "schemastore", "catalog-entries.json")));
        return [.. document.RootElement.EnumerateArray().Select(entry => entry.Clone())];
    }

    private static string SchemaId(string url)
    {
        string file = Path.Combine(Root, "schemas", url[(url.LastIndexOf('/') + 1)..]);
        Assert.True(File.Exists(file), $"'{url}' does not name a committed schema.");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(file));
        return schema.RootElement.GetProperty("$id").GetString() ?? string.Empty;
    }

    [Theory]
    [InlineData("NetPrints graph", "*.netpc.json")]
    [InlineData("NetPrints catalog configuration", "netprints.catalog.json")]
    public void EachEntryUrlEqualsTheCommittedSchemaIdAndMatchesTheExpectedFiles(string name, string fileMatch)
    {
        JsonElement entry = Assert.Single(Entries(), candidate => candidate.GetProperty("name").GetString() == name);

        string url = entry.GetProperty("url").GetString() ?? string.Empty;
        Assert.Equal(SchemaId(url), url);
        Assert.Equal([fileMatch], entry.GetProperty("fileMatch").EnumerateArray().Select(match => match.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("description").GetString()));
    }

    [Fact]
    public void TheCatalogHoldsExactlyTheTwoEntries() => Assert.Equal(2, Entries().Length);

    [Fact]
    public void ThePullRequestNoteStatesThatTheOwnerSubmitsIt()
    {
        string note = File.ReadAllText(Path.Combine(Root, "eng", "schemastore", "PULL_REQUEST.md"));

        Assert.Contains("**Owner action**: submit after the docs site serves both URLs; not done by agents", note.Split('\n').Select(line => line.TrimEnd()), StringComparer.Ordinal);
    }
}
