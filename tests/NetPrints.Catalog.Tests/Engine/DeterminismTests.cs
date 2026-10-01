using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T02: the same bytes on every run, whatever the order of the references and syntax trees.</summary>
public sealed class DeterminismTests
{
    private static string Write(CatalogBuildResult result) => CanonicalCatalogWriter.Write(result.Document);

    private static string FixtureJson(Func<IReadOnlyList<MetadataReference>, IEnumerable<MetadataReference>>? order = null)
    {
        (CSharpCompilation compilation, IAssemblySymbol assembly) = FixtureCatalog.OpenFixture(order);
        return Write(CatalogBuilder.Build(compilation, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi), FixtureCatalog.FixtureDocumentation(), new CatalogIdentity(FixtureCatalog.FixtureId)));
    }

    [Fact]
    public void TwoRunsProduceTheSameBytes() => Assert.Equal(FixtureJson(), FixtureJson());

    [Fact]
    public void ShuffledReferencesProduceTheSameBytes()
    {
        string expected = FixtureJson();

        Assert.Equal(expected, FixtureJson(references => references.Reverse()));
        Assert.Equal(expected, FixtureJson(references => references.OrderBy(r => r.Display, StringComparer.Ordinal)));
        Assert.Equal(expected, FixtureJson(references => references.Skip(7).Concat(references.Take(7))));
    }

    [Fact]
    public void ShuffledSyntaxTreesProduceTheSameBytes()
    {
        string[] sources =
        [
            "namespace Z { public class Zed { public Alpha.A Make() => default; public int Value { get; set; } } }",
            "namespace Alpha { public class A { public void M(int x, string y) { } public static A Create() => new(); } public enum Color { Red, Green } }",
            "namespace Alpha.Deep { public interface IThing { void Do(); } public struct S : IThing { public void Do() { } public int F; } }",
        ];

        string Build(IEnumerable<string> ordered)
        {
            CSharpCompilation compilation = FixtureCatalog.CreateCompilation(
                "Lib",
                FixtureCatalog.FrameworkReferences(),
                [.. ordered.Select(text => CSharpSyntaxTree.ParseText(text, cancellationToken: TestContext.Current.CancellationToken))]);
            return Write(CatalogBuilder.Build(compilation, [compilation.Assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi), XmlDocumentationSource.Empty, new CatalogIdentity()));
        }

        string expected = Build(sources);

        Assert.Equal(expected, Build(sources.Reverse()));
        Assert.Equal(expected, Build([sources[1], sources[2], sources[0]]));
        Assert.Contains("\"id\": \"lib\"", expected, StringComparison.Ordinal);
    }
}
