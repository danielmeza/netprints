using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Style;

/// <summary>NPS002: no blank line between adjacent members that need one.</summary>
public class Nps002Tests
{
    private const string Id = "NPS002";

    [Fact]
    public async Task TwoMethodsWithoutABlankLineAreReportedAtTheSecond()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                void A() { }
                {|NPS002:void|} B() { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task EveryMemberCategoryIsReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                int P { get; }
                {|NPS002:int|} this[int i] => i;
                {|NPS002:C|}() { }
                {|NPS002:public|} static C operator +(C a, C b) => a;
                {|NPS002:event|} System.Action E;
                {|NPS002:void|} M() { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task APropertyNextToAPrivateFieldIsReportedOnEitherSide()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;
                {|NPS002:int|} P { get; }
                {|NPS002:private|} int b;
            }
            """,
            Id);
    }

    [Fact]
    public async Task PublicFieldsAndConstsAreNeverGrouped()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                public int A;
                {|NPS002:public|} int B;
                {|NPS002:public|} const int K = 1;
                {|NPS002:private|} int c;
            }
            """,
            Id);
    }

    [Fact]
    public async Task AMemberWithAnAttributeACommentOrBracesNeedsABlankLine()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;
                {|NPS002:[|}System.Obsolete]
                private int b;
                // the c field
                {|NPS002:private|} int c;
                {|NPS002:private|} int d = 1; // trailing
                {|NPS002:private|} int e;
                {|NPS002:private|} System.Action f = () => { };
            }
            """,
            Id);
    }

    [Fact]
    public async Task ABlankLineSatisfiesTheRule()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                void A() { }

                void B() { }

                public int P { get; }

                // comment
                public int Q { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task GroupedNonPublicFieldsAndConstsPass()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;
                private int b;
                internal int c;
                protected int d;
                private const int K = 1;
                private protected int e;
                protected internal int f;
                int g;
            }
            """,
            Id);
    }

    [Fact]
    public async Task EnumMembersRecordParametersAndGlobalStatementsAreOutOfScope()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            enum E { A, B,
                C }

            enum F
            {
                A,
                B,
                C,
            }

            record R(int A, int B);

            System.Console.Write(1);
            System.Console.Write(2);
            """,
            Id);
    }

    [Fact]
    public async Task NamespaceMembersAreChecked()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            namespace N
            {
                class A { }
                {|NPS002:class|} B { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task InterfaceMembersAreChecked()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            interface I
            {
                void A();
                {|NPS002:void|} B();
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheFixInsertsABlankLineBeforeTheMember()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    void A() { }\n    void B() { }\n    void D() { }\n}\n",
            Id);

        Assert.Equal("class C\n{\n    void A() { }\n\n    void B() { }\n\n    void D() { }\n}\n", fixedText);
    }

    [Fact]
    public async Task TheFixInsertsTheBlankLineAboveTheMembersComment()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    void A() { }\n    // about B\n    void B() { }\n}\n",
            Id);

        Assert.Equal("class C\n{\n    void A() { }\n\n    // about B\n    void B() { }\n}\n", fixedText);
    }

    [Fact]
    public async Task TheFixKeepsCarriageReturnLineFeeds()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\r\n{\r\n    void A() { }\r\n    void B() { }\r\n}\r\n",
            Id);

        Assert.Equal("class C\r\n{\r\n    void A() { }\r\n\r\n    void B() { }\r\n}\r\n", fixedText);
    }

    [Fact]
    public async Task TheFixSeparatesMembersSharingALine()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    void A() { } void B() { }\n}\n",
            Id);

        Assert.Equal("class C\n{\n    void A() { }\n\n    void B() { }\n}\n", fixedText);
    }
}
