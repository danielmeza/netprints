using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH001: the null-forgiving operator (replaces <c>SourceHygieneTests.NoNullForgivingOperator</c>).</summary>
public class Nph001Tests
{
    private const string Id = "NPH001";

    [Fact]
    public async Task APlantedNullForgivingOperatorIsReported()
    {
        await HygieneHarness.AssertAsync(
            """
            #nullable enable
            class C
            {
                string? Find() => null;

                void M()
                {
                    string a = {|NPH001:Find()!|};
                    string b = {|NPH001:null!|};
                    string c = {|NPH001:default!|};
                }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ALogicalNegationAndANullableTypeAreNotReported()
    {
        await HygieneHarness.AssertAsync(
            """
            class C
            {
                int? M(bool flag) => !flag ? 1 : null;
            }
            """,
            Id);
    }

    [Fact]
    public async Task ALedgerSiteSurvivesAShiftedLineButNotAChangedOne()
    {
        const string Ledger = "NPH001 | tests/Fx/Fx.cs | var value = Lookup()!;";
        HygieneInputs inputs = new("/repo/tests/Fx/Fx.cs", Ledger);

        await HygieneHarness.AssertAsync("class Fx\n{\n    void Run()\n    {\n        var value = Lookup()!;\n    }\n}\n", Id, inputs);
        await HygieneHarness.AssertAsync("using System;\n\n// unrelated edit\nclass Fx\n{\n    void Run()\n    {\n        var value = Lookup()!;\n    }\n}\n", Id, inputs);
        await HygieneHarness.AssertAsync("class Fx\n{\n    void Run()\n    {\n        var value = {|NPH001:Other()!|};\n    }\n}\n", Id, inputs);
    }

    [Fact]
    public async Task ALedgerRowOfAnotherFileDoesNotCoverTheSite()
    {
        const string Ledger = "NPH001 | tests/Fx/Fx.cs | var value = Lookup()!;";

        await HygieneHarness.AssertAsync(
            "class Fx\n{\n    void Run()\n    {\n        var value = {|NPH001:Lookup()!|};\n    }\n}\n",
            Id,
            new HygieneInputs("/repo/tests/Fx/Other.cs", Ledger));
    }
}
