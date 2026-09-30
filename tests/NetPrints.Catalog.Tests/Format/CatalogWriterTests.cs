using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Format;

/// <summary>CT-T01: the canonical writing rules of contracts/catalog.md §1.</summary>
public sealed class CatalogWriterTests
{
    private static string Written => CanonicalCatalogWriter.Write(SampleCatalog.Build());

    [Fact]
    public void OutputEqualsTheCommittedGolden()
    {
        string path = TestPaths.GoldenPath(SampleCatalog.GoldenFileName);
        if (TestPaths.UpdateSnapshots)
        {
            File.WriteAllText(path, Written);
        }

        Assert.True(File.Exists(path), $"Missing golden {path}; regenerate with {TestPaths.UpdateSnapshotsVariable}=1");
        Assert.Equal(File.ReadAllText(path), Written);
    }

    [Fact]
    public void RootPropertiesComeInTheDocumentedOrder()
    {
        string[] lines = Written.Split('\n');
        Assert.Equal("{", lines[0]);
        Assert.Equal("  \"$schema\": \"https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json\",", lines[1]);
        Assert.Equal("  \"schemaVersion\": 1,", lines[2]);
        Assert.Equal("  \"id\": \"samplelib\",", lines[3]);
        Assert.Equal("  \"version\": \"1.2.3.4\",", lines[4]);
        Assert.Equal("  \"profile\": \"public-api\",", lines[5]);
        Assert.Equal("  \"assemblies\": [", lines[6]);
    }

    [Fact]
    public void UsesLfWithATrailingNewlineAndNoBom()
    {
        string text = Written;
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.NotEqual('﻿', text[0]);
    }

    [Fact]
    public void IndentsWithTwoSpacesAndNeverTabs()
    {
        string[] lines = Written.Split('\n');
        Assert.DoesNotContain(lines, line => line.StartsWith('\t'));
        Assert.All(lines, line => Assert.Equal(0, line.TakeWhile(c => c == ' ').Count() % 2));
    }

    [Fact]
    public void ParametersTypeRefsHintsAndObsoleteRecordsAreInline()
    {
        string text = Written;
        Assert.Contains("{ \"name\": \"a\", \"type\": { \"name\": \"Sample.Geometry.Vector2\" } },", text, StringComparison.Ordinal);
        Assert.Contains("\"baseType\": { \"name\": \"Sample.Base\", \"args\": [{ \"name\": \"System.String\" }] }", text, StringComparison.Ordinal);
        Assert.Contains("\"node\": { \"displayName\": \"Vector 2D\", \"category\": \"Math|Vectors\", \"keywords\": [\"point\", \"vec\"] },", text, StringComparison.Ordinal);
        Assert.Contains("\"obsolete\": { \"message\": \"Use \\\"Parse\\\" instead.\", \"error\": true },", text, StringComparison.Ordinal);
        Assert.Contains("\"default\": { \"type\": \"System.Int32\", \"value\": \"10\" }", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyOptionalPropertiesAreOmittedNeverNullEmptyOrFalse()
    {
        string text = Written;
        Assert.DoesNotContain("null", text, StringComparison.Ordinal);
        Assert.DoesNotContain(": []", text, StringComparison.Ordinal);
        Assert.DoesNotContain("false", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"namespace\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"obsolete\": {}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesQuoteBackslashAndControlCharactersButKeepsNonAscii()
    {
        string text = Written;
        Assert.Contains("Quote \\\" backslash \\\\ tab \\t newline \\n cr \\r bell \\u0001 unit \\u001f accents éü smile \U0001F600.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LowerCasesHexInUnicodeEscapes()
    {
        CatalogDocument document = SampleCatalog.Build() with
        {
            Types = [new CatalogType { Id = "T:A", Name = "A", Kind = CatalogTypeKind.Class, Summary = "\u001b\u000c" }],
        };
        Assert.Contains("\"summary\": \"\\u001b\\u000c\"", CanonicalCatalogWriter.Write(document), StringComparison.Ordinal);
    }

    [Fact]
    public void WritingTwiceGivesTheSameText()
    {
        Assert.Equal(Written, Written);
    }
}
