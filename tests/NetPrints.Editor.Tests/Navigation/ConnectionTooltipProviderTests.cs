using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class ConnectionTooltipProviderTests(TestEditor editor) : GraphTestBase(editor)
{
    private sealed class FixedProvider(int order, TooltipContent? content) : ITooltipProvider
    {
        public int Order { get; } = order;

        public TooltipContent? TryProvide(TooltipTarget target) => content;
    }

    private static string Ends(ConnectionViewModel c) => $"{c.Source.Node.Name}.{c.Source.Pin.Name} → {c.Target.Node.Name}.{c.Target.Pin.Name}";

    private TooltipContent? Provide(ConnectionViewModel cable) =>
        new ConnectionTooltipProvider().TryProvide(new TooltipTarget(TooltipTargetKind.Connection, cable));

    [Fact]
    public void AnExecutionCableShowsItsEndsAndTheWordExecution()
    {
        ConnectionViewModel cable = Graph.Connections.Single(c => c.Kind == PinKind.Exec);

        TooltipContent? content = Provide(cable);

        Assert.Equal(Ends(cable), content?.Title);
        Assert.Equal(["execution"], content?.Lines);
    }

    [Fact]
    public void ADataCableShowsTheDataType()
    {
        var write = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        GraphUtil.ConnectDataPins(LiteralNode.WithValue(Method, "x").OutputDataPins[0], write.ArgumentPins[0]);
        ConnectionViewModel cable = Graph.Connections.Single(c => c.Kind == PinKind.Data);

        TooltipContent? content = Provide(cable);

        Assert.Equal(Ends(cable), content?.Title);
        Assert.Equal([StringType.ToString()], content?.Lines);
    }

    [Fact]
    public void WithoutLoadedDocumentationThereIsNone() =>
        Assert.Null(Provide(Graph.Connections.Single(c => c.Kind == PinKind.Exec))?.Documentation);

    [Fact]
    public void OtherTargetsGetNothing() =>
        Assert.Null(new ConnectionTooltipProvider().TryProvide(new TooltipTarget(TooltipTargetKind.Pin, Graph.Connections.First().Source)));

    [Fact]
    public void ProvidersAreAskedInOrderAndTheFirstNonNullContentWins()
    {
        var target = new TooltipTarget(TooltipTargetKind.Connection, new object());
        var late = new FixedProvider(5, new TooltipContent("late", []));
        var early = new FixedProvider(1, new TooltipContent("early", []));
        var silent = new FixedProvider(0, null);

        Assert.Equal("early", TooltipResolver.Resolve([late, early, silent], target)?.Title);
        Assert.Equal("late", TooltipResolver.Resolve([late, silent], target)?.Title);
        Assert.Null(TooltipResolver.Resolve([silent], target));
    }

    [Fact]
    public void TheBuiltInProviderIsRegistered()
    {
        var registry = new ContributionRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger<ContributionRegistry>.Instance);

        BuiltInContributions.Register(registry);

        Assert.Contains(registry.TooltipProviders, provider => provider is ConnectionTooltipProvider);
    }
}
