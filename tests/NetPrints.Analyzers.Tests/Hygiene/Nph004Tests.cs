using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH004: a suppression the ledger does not list (replaces <c>SourceHygieneTests.NoUnlistedSuppressions</c>).</summary>
public class Nph004Tests
{
    private const string Id = "NPH004";

    private const string Ledger = "NPH004 | src/Lib/Graph.cs | Moved:VSTHRD100";

    private static readonly HygieneInputs Listed = new("/repo/src/Lib/Graph.cs", Ledger);

    [Fact]
    public async Task APlantedPragmaIsReported()
    {
        await HygieneHarness.AssertAsync(
            """
            {|NPH004:#pragma warning disable CS1591|}
            class C { }
            {|NPH004:#pragma warning restore CS1591|}
            """,
            Id);
    }

    [Fact]
    public async Task ANullableEscapeIsReportedButEnablingIsNot()
    {
        await HygieneHarness.AssertAsync(
            """
            #nullable enable
            class A { }
            {|NPH004:#nullable disable|}
            class B { }
            {|NPH004:#nullable restore|}
            """,
            Id);
    }

    [Fact]
    public async Task AnUnlistedSuppressMessageIsReportedInEveryForm()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                [{|NPH004:SuppressMessage("Usage", "CA2200", Justification = "ADR-0003: x")|}]
                void A() { }

                [{|NPH004:SuppressMessageAttribute("Usage", "CA2200")|}]
                void B() { }

                [{|NPH004:System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2200")|}]
                int P => 1;
            }
            """,
            Id);
    }

    [Fact]
    public async Task ARowOfTheOtherMemberOrRuleDoesNotCoverTheAttribute()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                [{|NPH004:SuppressMessage("Usage", "VSTHRD100", Justification = "ADR-0003: x")|}]
                void Other() { }

                [{|NPH004:SuppressMessage("Usage", "IDISP004", Justification = "ADR-0003: x")|}]
                void Moved() { }
            }
            """,
            Id,
            Listed);
    }

    [Fact]
    public async Task AListedSuppressMessageWithAnAdrJustificationIsAccepted()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                [SuppressMessage("Usage", "VSTHRD100", Justification = "ADR-0003: the analyzer cannot see the handler")]
                void Moved() { }
            }
            """,
            Id,
            Listed);
    }

    [Fact]
    public async Task AListedSuppressMessageWithoutTheAdrJustificationIsReported()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                [{|NPH004:SuppressMessage("Usage", "VSTHRD100")|}]
                void Moved() { }
            }
            """,
            Id,
            Listed);
    }

    [Fact]
    public async Task AListedRowOfAnotherFileDoesNotCoverTheAttribute()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                [{|NPH004:SuppressMessage("Usage", "VSTHRD100", Justification = "ADR-0003: x")|}]
                void Moved() { }
            }
            """,
            Id,
            new HygieneInputs("/repo/src/Lib/Other.cs", Ledger));
    }
}
