using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Style;

/// <summary>NPS003: grouped non-public fields, a suggestion that never fails a build.</summary>
public class Nps003Tests
{
    private const string Id = "NPS003";

    [Fact]
    public async Task GroupedPrivateFieldsAreReportedAtEachFollowingField()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;
                {|NPS003:private|} int b;
                {|NPS003:private|} int c;
            }
            """,
            Id);
    }

    [Fact]
    public async Task InternalProtectedAndConstGroupsAreReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                internal int a;
                {|NPS003:protected|} int b;
                {|NPS003:private|} const int K = 1;
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheSeverityIsASuggestionThatNeverFailsABuild()
    {
        var diagnostics = await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;
                {|NPS003:private|} int b;
            }
            """,
            Id);

        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
            Assert.False(diagnostic.IsWarningAsError);
        });
    }

    [Fact]
    public async Task FieldsSeparatedByABlankLineAreNotReported()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                private int a;

                private int b;
            }
            """,
            Id);
    }

    [Fact]
    public async Task PublicFieldsAndMembersWithCommentsAttributesOrBracesAreNps002NotNps003()
    {
        await AnalyzerHarness.AssertDiagnosticsAsync(
            """
            class C
            {
                public int a;
                public int b;
                // comment
                private int c;
                private int d;
                [System.Obsolete]
                private int e;
                void M() { }
                void N() { }
            }
            """,
            Id);
    }

    [Fact]
    public async Task TheFixInsertsABlankLineBetweenTheFields()
    {
        string fixedText = await AnalyzerHarness.FixAsync(
            "class C\n{\n    private int a;\n    private int b;\n    private int c;\n}\n",
            Id);

        Assert.Equal("class C\n{\n    private int a;\n\n    private int b;\n\n    private int c;\n}\n", fixedText);
    }
}
