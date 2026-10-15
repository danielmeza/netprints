using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Tests.Navigation;

public class MatchRankingTests
{
    [Theory]
    [InlineData("Save all", "sa", MatchRanking.Prefix)]
    [InlineData("Undo save", "sa", MatchRanking.WordStart)]
    [InlineData("ToggleSave", "sa", MatchRanking.WordStart)]
    [InlineData("Resave", "sa", MatchRanking.Substring)]
    [InlineData("Close tab", "sa", MatchRanking.NoMatch)]
    [InlineData("Anything", "", MatchRanking.Prefix)]
    [InlineData("SAVE", "save", MatchRanking.Prefix)]
    [InlineData("Add node…", "node", MatchRanking.WordStart)]
    public void TheRankIsPrefixThenWordStartThenSubstring(string name, string query, int expected) =>
        Assert.Equal(expected, MatchRanking.Rank(name, query));

    [Fact]
    public void ResultsAreOrderedByRankThenName()
    {
        string[] names = ["Resave", "Undo save", "Save b", "Save a", "Close"];

        string[] ordered = [.. MatchRanking.Order(names, name => name, "sa")];

        Assert.Equal(["Save a", "Save b", "Undo save", "Resave"], ordered);
    }
}
