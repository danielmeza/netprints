using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class ConnectionTooltipProviderTests(TestEditor editor) : GraphTestBase(editor)
{
    private sealed class FixedProvider(int order, TooltipContent? content) : ITooltipProvider
    {
        public int Order { get; } = order;

        public TooltipContent? TryProvide(TooltipTarget target) => content;
    }

    private sealed class RecordingProvider : ITooltipProvider
    {
        public List<object> Subjects { get; } = [];

        public int Order => -10;

        public TooltipContent? TryProvide(TooltipTarget target)
        {
            Subjects.Add(target.Subject);
            return null;
        }
    }

    private sealed class FixedContexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => new(new FakeShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    private static string Ends(ConnectionViewModel c) => $"{c.Source.Node.Name}.{c.Source.Pin.Name} → {c.Target.Node.Name}.{c.Target.Pin.Name}";

    private TooltipContent? Provide(ConnectionViewModel cable) =>
        ((IEditorTooltipProvider)new ConnectionTooltipProvider()).TryProvide(new TooltipTarget(TooltipTargetKind.Connection, new TooltipConnection(cable.Source.Pin, cable.Target.Pin)), cable);

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
        Assert.Null(new ConnectionTooltipProvider().TryProvide(new TooltipTarget(TooltipTargetKind.Pin, Graph.Connections.First().Source.Pin)));

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
    public void AnExtensionTooltipProviderGetsTheModelPinsOfTheConnection()
    {
        var registry = new ContributionRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger<ContributionRegistry>.Instance);
        var recording = new RecordingProvider();
        registry.AddTooltipProvider(recording);
        registry.Freeze();
        Graph.Commands = new CommandInvoker(registry, new FixedContexts(), _ => { });
        ConnectionViewModel cable = Graph.Connections.Single(c => c.Kind == PinKind.Exec);

        _ = cable.ToolTip;

        object subject = Assert.Single(recording.Subjects);
        Assert.NotEqual(typeof(ConnectionViewModel).Assembly, subject.GetType().Assembly);
        TooltipConnection pins = Assert.IsType<TooltipConnection>(subject);
        Assert.Same(cable.Source.Pin, pins.Source);
        Assert.Same(cable.Target.Pin, pins.Target);
    }

    [Fact]
    public void TheBuiltInProviderIsRegistered()
    {
        var registry = new ContributionRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger<ContributionRegistry>.Instance);

        BuiltInContributions.Register(registry);

        Assert.Contains(registry.TooltipProviders, provider => provider is ConnectionTooltipProvider);
    }
}
