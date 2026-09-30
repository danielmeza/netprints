using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T07: editing an unrelated syntax tree does not rebuild the referenced-assembly catalog.</summary>
public sealed class IncrementalTests
{
    private const string ReferencedStep = "ReferencedCatalog";

    private const string OutputsStep = "CatalogOutputs";

    private const string Request = "[assembly: NetPrints.Annotations.NetPrintsCatalog(\"CatalogFixtureLib\")]";

    private static readonly IncrementalStepRunReason[] Reused = [IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged];

    private static IEnumerable<IncrementalStepRunReason> Reasons(GeneratorDriverRunResult result, string step) =>
        result.Results[0].TrackedSteps[step].SelectMany(s => s.Outputs).Select(o => o.Reason);

    private static (GeneratorDriverRunResult First, GeneratorDriverRunResult Second) RunTwice(string firstUnrelated, string secondUnrelated)
    {
        CSharpCompilation compilation = GeneratorFixtures.CompileConsumer(Request);
        SyntaxTree unrelated = CSharpSyntaxTree.ParseText(firstUnrelated, new CSharpParseOptions(LanguageVersion.Latest));
        compilation = compilation.AddSyntaxTrees(unrelated);

        GeneratorDriver driver = GeneratorTestHost.CreateDriver(compilation, GeneratorFixtures.FixtureInputs(), trackSteps: true);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult first = driver.GetRunResult();

        compilation = compilation.ReplaceSyntaxTree(unrelated, CSharpSyntaxTree.ParseText(secondUnrelated, new CSharpParseOptions(LanguageVersion.Latest)));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        return (first, driver.GetRunResult());
    }

    [Fact]
    public void EditingAnUnrelatedTreeLeavesTheReferencedCatalogStepCachedOrUnchanged()
    {
        var (first, second) = RunTwice("class Unrelated { }", "class Unrelated { void Edited() { } }");

        Assert.NotEmpty(Reasons(first, ReferencedStep));
        Assert.All(Reasons(first, ReferencedStep), reason => Assert.Equal(IncrementalStepRunReason.New, reason));
        Assert.NotEmpty(Reasons(second, ReferencedStep));
        Assert.All(Reasons(second, ReferencedStep), reason => Assert.Contains(reason, Reused));
        Assert.All(Reasons(second, OutputsStep), reason => Assert.Contains(reason, Reused));
        Assert.Equal(first.GeneratedTrees.Length, second.GeneratedTrees.Length);
    }

    [Fact]
    public void ChangingTheRequestRebuildsTheCatalog()
    {
        CSharpCompilation compilation = GeneratorFixtures.CompileConsumer(Request);
        GeneratorDriver driver = GeneratorTestHost.CreateDriver(compilation, GeneratorFixtures.FixtureInputs(), trackSteps: true).RunGenerators(compilation, TestContext.Current.CancellationToken);

        CSharpCompilation changed = GeneratorFixtures.CompileConsumer(Request.Replace("\"CatalogFixtureLib\"", "\"CatalogFixtureLib\", Id = \"other\"", System.StringComparison.Ordinal));
        driver = driver.RunGenerators(changed, TestContext.Current.CancellationToken);

        Assert.All(Reasons(driver.GetRunResult(), ReferencedStep), reason => Assert.Contains(reason, new[] { IncrementalStepRunReason.Modified, IncrementalStepRunReason.New }));
        Assert.Single(GeneratorTestHost.GeneratedSources(driver), s => s.HintName == "NetPrintsCatalog.other.g.cs");
        Assert.DoesNotContain(GeneratorTestHost.GeneratedSources(driver), s => s.HintName == "NetPrintsCatalog.catalogfixturelib.g.cs");
    }
}
