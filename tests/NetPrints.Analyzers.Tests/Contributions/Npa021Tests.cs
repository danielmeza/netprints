using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA021: a gesture parses, and a single-key gesture is not a default of a Global command.</summary>
public class Npa021Tests
{
    private const string Id = "NPA021";

    [Theory]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+S")]
    [InlineData("Hyper+S")]
    [InlineData("Ctrl+S+D")]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+F25")]
    public async Task AGestureThatDoesNotParseIsReported(string gesture)
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use($"            _ = CommandGesture.Parse({{|NPA021:\"{gesture}\"|}});"), Id);
    }

    [Theory]
    [InlineData("Ctrl+S")]
    [InlineData("ctrl+shift+b")]
    [InlineData("Alt+Left")]
    [InlineData("F5")]
    [InlineData("Shift+F5")]
    [InlineData("Cmd+Del")]
    [InlineData("Ctrl+0")]
    public async Task AGestureThatParsesIsAccepted(string gesture)
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(ContributionSamples.Use($"            _ = CommandGesture.Parse(\"{gesture}\");"), Id);
    }

    [Fact]
    public async Task ASingleKeyDefaultOfAGlobalCommandIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.b", "x", handler, DefaultGestures: [{|NPA021:new(CommandModifiers.None, CommandKey.Delete)|}]);
                            _ = new CommandDescriptor("a.c", "x", handler, DefaultGestures: [{|NPA021:new(CommandModifiers.Shift, CommandKey.S)|}], Scope: CommandScope.Global);
                            _ = new CommandDescriptor("a.d", "x", handler, DefaultGestures: [{|NPA021:CommandGesture.Parse("A")|}]);
                """),
            Id);
    }

    [Fact]
    public async Task ASingleKeyDefaultIsAcceptedInAScopeAndForFunctionKeys()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("a.b", "x", handler, DefaultGestures: [new(CommandModifiers.None, CommandKey.Delete)], Scope: CommandScope.Graph);
                            _ = new CommandDescriptor("a.c", "x", handler, DefaultGestures: [new(CommandModifiers.None, CommandKey.F5), new(CommandModifiers.Ctrl, CommandKey.S)]);
                            _ = new CommandDescriptor("a.d", "x", handler, DefaultGestures: [CommandGesture.Parse("Ctrl+Shift+B")]);
                            _ = new CommandDescriptor("a.e", "x", handler, DefaultGestures: null);
                """),
            Id);
    }
}
