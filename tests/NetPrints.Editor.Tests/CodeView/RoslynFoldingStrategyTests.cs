using NetPrints.Editor.CodeView;

namespace NetPrints.Editor.Tests.CodeView;

/// <summary><see cref="RoslynFoldingStrategy"/> (editor-services.md §3): type and member folding, ED-T01.</summary>
public sealed class RoslynFoldingStrategyTests
{
    [Fact]
    public void FoldsTheTypeBodyAndTheMethodBody()
    {
        const string code = """
            namespace N
            {
                public class C
                {
                    public void M()
                    {
                        System.Console.WriteLine("hi");
                    }
                }
            }
            """;

        IReadOnlyList<FoldingRange> foldings = RoslynFoldingStrategy.ComputeFoldings(code);

        Assert.Equal(2, foldings.Count);
        Assert.All(foldings, folding => Assert.Equal(RoslynFoldingStrategy.CollapsedPlaceholder, folding.Title));
        Assert.True(foldings[0].Start < foldings[1].Start, "the class's folding starts before the method's");
        Assert.True(foldings[0].End > foldings[1].End, "the class's folding contains the method's");
    }

    [Fact]
    public void ABodyWithNothingBetweenItsBracesIsNotFolded()
    {
        Assert.Empty(RoslynFoldingStrategy.ComputeFoldings("public class C {}"));
    }

    [Fact]
    public void AnEmptyMethodBodyInsideANonEmptyTypeFoldsOnlyTheType()
    {
        FoldingRange folding = Assert.Single(RoslynFoldingStrategy.ComputeFoldings("public class C { public void M() {} }"));
        Assert.Equal(RoslynFoldingStrategy.CollapsedPlaceholder, folding.Title);
    }

    [Fact]
    public void CodeWithNoBracesFoldsNothing()
    {
        Assert.Empty(RoslynFoldingStrategy.ComputeFoldings(string.Empty));
    }
}
