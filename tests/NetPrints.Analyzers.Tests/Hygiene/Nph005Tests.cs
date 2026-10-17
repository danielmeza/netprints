using System.Collections.Generic;
using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH005: <c>ConfigureAwait(false)</c> in UI-bound code (replaces <c>SourceHygieneTests.UiBoundCodeNeverUsesConfigureAwaitFalse</c>).</summary>
public class Nph005Tests
{
    private const string Id = "NPH005";

    private const string OptionKey = "dotnet_diagnostic.VSTHRD111.severity";

    private static HygieneInputs Scope(string severity) => new(Options: new Dictionary<string, string> { [OptionKey] = severity });

    [Fact]
    public async Task APlantedConfigureAwaitFalseIsReportedWhereTheRuleIsOff()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Threading.Tasks;

            class C
            {
                async Task M()
                {
                    await Task.Delay(1).{|NPH005:ConfigureAwait(false)|};
                }
            }
            """,
            Id,
            Scope("none"));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("warning")]
    public async Task ConfigureAwaitFalseIsAcceptedWhereTheRuleIsOn(string severity)
    {
        await HygieneHarness.AssertAsync(
            "using System.Threading.Tasks;\nclass C\n{\n    async Task M() => await Task.Delay(1).ConfigureAwait(false);\n}\n",
            Id,
            Scope(severity));
    }

    [Fact]
    public async Task AFileWithoutTheOptionIsNotUiBound()
    {
        await HygieneHarness.AssertAsync(
            "using System.Threading.Tasks;\nclass C\n{\n    async Task M() => await Task.Delay(1).ConfigureAwait(false);\n}\n",
            Id);
    }

    [Fact]
    public async Task ConfigureAwaitTrueAndOtherArgumentsAreAcceptedInUiBoundCode()
    {
        await HygieneHarness.AssertAsync(
            """
            using System.Threading.Tasks;

            class C
            {
                async Task M(bool flag)
                {
                    await Task.Delay(1).ConfigureAwait(true);
                    await Task.Delay(1).ConfigureAwait(flag);
                }
            }
            """,
            Id,
            Scope("none"));
    }
}
