using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Style;

/// <summary>NPS001: an attribute list shares a line with its member or with another attribute list.</summary>
public class Nps001Tests
{
    private const string Id = "NPS001";

    [Fact]
    public async Task AnAttributeOnTheMembersLineIsReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                {|NPS001:[System.Obsolete]|} public int X;
            }
            """,
            Id);
    }

    [Fact]
    public async Task TwoAttributeListsOnOneLineAreBothReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                {|NPS001:[System.Obsolete]|} {|NPS001:[System.CLSCompliant(true)]|} void M() { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheOneOnItsOwnLineIsNotReportedWhenTheNextSharesALine()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                [System.Obsolete]
                {|NPS001:[System.CLSCompliant(true)]|} void M() { }
            }
            """,
            Id);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("struct")]
    [InlineData("interface")]
    [InlineData("record")]
    public async Task AnInlineAttributeOnATypeIsReported(string keyword)
    {
        await AnalyzerHarness.AssertDiagnosticsAsync($"{{|NPS001:[System.Obsolete]|}} {keyword} T {{ }}", Id);
    }

    [Fact]
    public async Task AnInlineAttributeOnEveryMemberKindIsReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                {|NPS001:[System.Obsolete]|} C() { }

                {|NPS001:[System.Obsolete]|} int P { get; }

                {|NPS001:[System.Obsolete]|} event System.Action E;

                {|NPS001:[System.Obsolete]|} enum N { A }

                {|NPS001:[System.Obsolete]|} delegate void D();
            }
            """,
            Id);
    }

    [Fact]
    public async Task AnAttributeOnItsOwnLineIsAccepted()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                [System.Obsolete]
                public int X;

                [System.Obsolete]
                [System.CLSCompliant(true)]
                void M() { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task SeveralAttributesInOneListAreAccepted()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                [System.Obsolete, System.CLSCompliant(true)]
                public int X;
            }
            """,
            Id);
    }

    [Fact]
    public async Task EnumMembersParametersAndRecordParametersAreOutOfScope()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            enum E
            {
                [System.Obsolete] A,
                [System.Obsolete] B,
            }

            record R([property: System.Obsolete] int X);

            class C
            {
                void M([System.Obsolete] int x) { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AnAttributeSplitAcrossLinesButSharingTheClosingLineIsReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                {|NPS001:[System.Diagnostics.CodeAnalysis.SuppressMessage("a",
                    "b")]|} int X;
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheFixPutsTheMemberOnTheNextLineWithTheSameIndent()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    [System.Obsolete] public int X;\n}\n",
            Id);

        Assert.Equal("class C\n{\n    [System.Obsolete]\n    public int X;\n}\n", fixedText);
    }

    [Fact]
    public async Task TheFixSplitsTwoListsOnOneLine()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    [System.Obsolete]   [System.CLSCompliant(true)] void M() { }\n}\n",
            Id);

        Assert.Equal("class C\n{\n    [System.Obsolete]\n    [System.CLSCompliant(true)]\n    void M() { }\n}\n", fixedText);
    }

    [Fact]
    public async Task TheFixKeepsCarriageReturnLineFeeds()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\r\n{\r\n    [System.Obsolete] public int X;\r\n}\r\n",
            Id);

        Assert.Equal("class C\r\n{\r\n    [System.Obsolete]\r\n    public int X;\r\n}\r\n", fixedText);
    }

    [Fact]
    public async Task TheFixLeavesACommentBetweenTheListAndTheMemberAlone()
    {
        const string Source = "class C\n{\n    [System.Obsolete] /* why */ public int X;\n}\n";

        string fixedText = await AnalyzerHarness.FixAsync(Source, Id);

        Assert.Equal(Source, fixedText);
    }
}
