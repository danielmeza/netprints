using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH003: a code literal outside its constants class (replaces <c>SourceHygieneTests.NoRawDiagnosticCodeLiteralsOutsideTheirConstants</c>).</summary>
public class Nph003Tests
{
    private const string Id = "NPH003";

    private const string Codes = "/repo/src/NetPrints.Core/Translator/TranslationDiagnosticCodes.cs";

    [Theory]
    [InlineData("NPT001")]
    [InlineData("NPD1000")]
    [InlineData("NPX001")]
    [InlineData("NPW003")]
    [InlineData("NPC002")]
    [InlineData("NPS001")]
    [InlineData("NPA040")]
    [InlineData("NPH006")]
    [InlineData("NPI010")]
    [InlineData("NPK001")]
    [InlineData("NPR001")]
    [InlineData("NPXE0004")]
    public async Task APlantedCodeLiteralIsReported(string code)
    {
        await HygieneHarness.AssertAsync($"class C\n{{\n    string M() => {{|NPH003:\"{code}\"|}};\n}}\n", Id);
    }

    [Theory]
    [InlineData("NPT01")]
    [InlineData("NPXE001")]
    [InlineData("NPQ001")]
    [InlineData("NPT001 failed")]
    [InlineData("")]
    public async Task OtherTextIsNotACode(string text)
    {
        await HygieneHarness.AssertAsync($"class C\n{{\n    string M() => \"{text}\";\n}}\n", Id);
    }

    [Fact]
    public async Task ADeclarationInADesignatedFileIsAccepted()
    {
        const string Source = "class Codes\n{\n    public const string Failed = \"NPT001\";\n}\n";

        await HygieneHarness.AssertAsync(Source, Id, new HygieneInputs(Codes));
        await HygieneHarness.AssertAsync(Source, Id, new HygieneInputs("/repo/src/NetPrints.Core/Projects/ProjectMessage.cs"));
        await HygieneHarness.AssertAsync(Source, Id, new HygieneInputs("/repo/src/NetPrints.Authoring/Pins/NpaDiagnosticCodes.cs"));
    }

    [Fact]
    public async Task ADeclarationOutsideADesignatedFileIsReported()
    {
        await HygieneHarness.AssertAsync(
            "class C\n{\n    public const string Failed = {|NPH003:\"NPT001\"|};\n}\n",
            Id,
            new HygieneInputs("/repo/src/NetPrints.Core/Other.cs"));
    }

    [Fact]
    public async Task ASecondDeclarationOfOneCodeInAProjectIsReported()
    {
        await HygieneHarness.AssertAsync(
            "class Codes\n{\n    public const string A = \"NPT001\";\n\n    public const string B = {|NPH003:\"NPT001\"|};\n}\n",
            Id,
            new HygieneInputs(Codes));
    }

    [Fact]
    public async Task AFieldThatIsNotAConstantIsAUse()
    {
        await HygieneHarness.AssertAsync(
            "class Codes\n{\n    public static readonly string A = {|NPH003:\"NPT001\"|};\n}\n",
            Id,
            new HygieneInputs(Codes));
    }
}
