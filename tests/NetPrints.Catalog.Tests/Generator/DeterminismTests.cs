using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T14: the generated sources do not depend on the run, the order of the syntax trees or the order of the references.</summary>
public sealed class DeterminismTests
{
    private const string Own = """
        using NetPrints.Annotations;

        namespace Own
        {
            [NetPrintsType]
            public class First
            {
                [NetPrintsNode]
                public void A() { }
            }
        }
        """;

    private const string OwnToo = """
        using NetPrints.Annotations;

        namespace Own
        {
            [NetPrintsType]
            public class Second
            {
                [NetPrintsNode]
                public void B() { }
            }
        }
        """;

    private const string Requests = """
        [assembly: NetPrints.Annotations.NetPrintsCatalog("CatalogFixtureLib", AccessorName = "Fixture")]
        [assembly: NetPrints.Annotations.NetPrintsCatalog("CatalogFixtureLib", Id = "flags", Profile = "fixture-flags.npprofile.json", AccessorName = "Flags")]
        """;

    private static IReadOnlyList<(string HintName, string Text)> Generate(IEnumerable<string> sources, IEnumerable<MetadataReference> references)
    {
        CSharpCompilation compilation = GeneratorTestHost.Compile("Consumer", sources, LanguageVersion.Latest, references);
        GeneratorDriver driver = GeneratorTestHost.CreateDriver(compilation, GeneratorFixtures.FixtureInputs()).RunGenerators(compilation, TestContext.Current.CancellationToken);
        return GeneratorTestHost.GeneratedSources(driver);
    }

    private static IReadOnlyList<MetadataReference> References() =>
        [.. FixtureCatalog.FrameworkReferences(), MetadataReference.CreateFromFile(FixtureLibrary.AssemblyPath)];

    [Fact]
    public void TwoRunsProduceIdenticalSources()
    {
        IReadOnlyList<(string HintName, string Text)> first = Generate([Own, OwnToo, Requests], References());
        IReadOnlyList<(string HintName, string Text)> second = Generate([Own, OwnToo, Requests], References());

        Assert.Contains(first, s => s.HintName == "NetPrintsCatalog.Self.g.cs");
        Assert.Equal(first, second);
    }

    [Fact]
    public void TheOrderOfTreesAndReferencesDoesNotChangeTheSources()
    {
        IReadOnlyList<(string HintName, string Text)> ordered = Generate([Own, OwnToo, Requests], References());
        IReadOnlyList<(string HintName, string Text)> shuffled = Generate([Requests, OwnToo, Own], [.. References().Reverse()]);

        Assert.Equal(ordered, shuffled);
        Assert.Equal(3, ordered.Count(s => s.HintName.StartsWith("NetPrintsCatalog.", System.StringComparison.Ordinal)));
    }
}
