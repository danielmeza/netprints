using System.Collections.Generic;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Tests.Run;

/// <summary>FR-054, SC-008, run-profiles.md §7.2: <c>Split</c> and <c>Join</c> follow <c>CommandLineToArgvW</c> on every OS.</summary>
public class CommandLineSplitterTests
{
    public static TheoryData<string[]> RoundTripCases() => new()
    {
        { [] },
        { [""] },
        { ["a"] },
        { ["a", "b"] },
        { ["a b"] },
        { ["a\tb"] },
        { ["\""] },
        { ["a\"b"] },
        { ["a\"b c"] },
        { ["\\"] },
        { ["a\\"] },
        { ["a b\\"] },
        { ["\\\\"] },
        { ["a\\\"b"] },
        { ["a\\\\\"b"] },
        { ["a\\b"] },
        { ["a\\\\b"] },
        { ["", ""] },
        { ["", "a", ""] },
        { [" "] },
        { ["\t"] },
        { ["  a  "] },
        { ["ñandú"] },
        { ["日本語 テキスト"] },
        { ["😀"] },
        { ["a\"\"b"] },
        { ["\"\""] },
        { ["\"a b\""] },
        { ["C:\\Program Files\\App\\"] },
        { ["C:\\Program Files\\App\\", "x"] },
        { ["--name=a b", "--flag"] },
        { ["\\\" "] },
        { ["\\\\\" x\\\\"] },
        { ["a\"", "\"b"] },
        { ["-e", "echo \"hi there\""] },
        { ["a\nb"] },
        { ["{\"k\": \"v\"}"] },
        { ["a", "", "b c", "d\"e", "f\\"] },
    };

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void RoundTrip(string[] arguments)
    {
        string joined = CommandLineSplitter.Join(arguments);

        Assert.Equal(arguments, CommandLineSplitter.Split(joined));
    }

    [Fact]
    public void ThereAreAtLeastThirtyRoundTripCases()
    {
        Assert.True(RoundTripCases().Count >= 30);
    }

    public static TheoryData<string, string[]> SplitCases() => new()
    {
        { "", [] },
        { "   ", [] },
        { "a", ["a"] },
        { "a b", ["a", "b"] },
        { "a \t  b", ["a", "b"] },
        { "  a b  ", ["a", "b"] },
        { "\"a b\" c", ["a b", "c"] },
        { "\"a b", ["a b"] },
        { "a\"b c\"d", ["ab cd"] },
        { "a\\\\\\\"b", ["a\\\"b"] },
        { "\"a\"\"b\"", ["a\"b"] },
        { "\"\"", [""] },
        { "a \"\" b", ["a", "", "b"] },
        { "a\\\\b", ["a\\\\b"] },
        { "a\\b", ["a\\b"] },
        { "a\\\"b", ["a\"b"] },
        { "a\\\\\"b c\"", ["a\\b c"] },
        { "\"a\\\\\"", ["a\\"] },
        { "\\\\\\\\", ["\\\\\\\\"] },
        { "a\\", ["a\\"] },
    };

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void SplitFollowsTheRulesOfCommandLineToArgvW(string commandLine, string[] expected)
    {
        Assert.Equal(expected, CommandLineSplitter.Split(commandLine));
    }

    public static TheoryData<string[], string> JoinCases() => new()
    {
        { [], "" },
        { ["a"], "a" },
        { ["a", "b"], "a b" },
        { ["a b", "c"], "\"a b\" c" },
        { [""], "\"\"" },
        { ["a\"b"], "\"a\\\"b\"" },
        { ["a\\"], "a\\" },
        { ["a b\\"], "\"a b\\\\\"" },
        { ["a\\\"b"], "\"a\\\\\\\"b\"" },
        { ["a\tb"], "\"a\tb\"" },
        { ["a\\b"], "a\\b" },
    };

    [Theory]
    [MemberData(nameof(JoinCases))]
    public void JoinQuotesOnlyWhenNeeded(string[] arguments, string expected)
    {
        Assert.Equal(expected, CommandLineSplitter.Join(arguments));
    }

    [Fact]
    public void SplitReturnsAReadOnlyListInOrder()
    {
        IReadOnlyList<string> arguments = CommandLineSplitter.Split("one two three");

        Assert.Equal(3, arguments.Count);
        Assert.Equal("three", arguments[2]);
    }
}
