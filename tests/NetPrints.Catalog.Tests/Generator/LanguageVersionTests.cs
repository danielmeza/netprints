using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T08: a consumer at C# 7.3 (the netstandard2.0 default) compiles everything the generator adds.</summary>
public sealed class LanguageVersionTests
{
    private const string Source = """
        using NetPrints.Annotations;

        [assembly: NetPrintsCatalog("CatalogFixtureLib", AccessorName = "Fixture")]

        namespace Consumer.Lib
        {
            [NetPrintsType(DisplayName = "Thing")]
            public class Thing
            {
                [NetPrintsNode(Category = "Actions")]
                public void Do() { }
            }
        }
        """;

    [Fact]
    public void TheGeneratedSourcesCompileAtCSharp7()
    {
        CSharpCompilation compilation = GeneratorFixtures.CompileConsumer(Source, LanguageVersion.CSharp7_3);

        var (output, diagnostics) = GeneratorTestHost.Run(compilation, GeneratorFixtures.FixtureInputs());

        Assert.Empty(GeneratorTestHost.Errors(output));
        Assert.DoesNotContain(diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(2, GeneratorTestHost.EmbeddedCatalogs(output).Count);
    }

    [Fact]
    public void TheGeneratedSourcesCompileAtCSharp7WithoutARootNamespace()
    {
        CSharpCompilation compilation = GeneratorFixtures.CompileConsumer(Source, LanguageVersion.CSharp7_3);

        var (output, _) = GeneratorTestHost.Run(compilation, GeneratorFixtures.FixtureInputs(rootNamespace: null));

        Assert.Empty(GeneratorTestHost.Errors(output));
        Assert.NotNull(output.GetTypeByMetadataName("NetPrintsCatalogs"));
    }
}
