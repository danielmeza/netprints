using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>CD-19, FR-021, FR-034, ADR-0020: a panel and a start page tile factory resolve the contributing extension's own services from their context, and not another extension's.</summary>
public sealed class FactoryServiceProviderTests : IAsyncDisposable
{
    private const string Alpha = "fx.alpha";
    private const string Beta = "fx.beta";

    private readonly ExtensionContributionRecorder recorder = new();
    private readonly TestEditor editor;
    private readonly ContributionRegistry registry = new(NullLogger<ContributionRegistry>.Instance);
    private readonly ShellViewModel shell;
    private readonly ShellProjectActions actions;

    public FactoryServiceProviderTests()
    {
        var host = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, Entry(Alpha, new AlphaExtension()), Entry(Beta, new BetaExtension())]) { EditorContributions = recorder.Factory },
            NullLoggerFactory.Instance);
        editor = TestEditor.Create(TestEditor.CreateReflectionHost, host, recorder: recorder);
        BuiltInContributions.Register(registry);
        recorder.ApplyToAppGroup(registry);
        registry.Freeze();
        shell = new ShellViewModel(registry, PanelFactoryContext.Detached, new FakeTimeProvider(), new InlineDispatcher(), editor.Context);
        actions = new ShellProjectActions(editor.Context, shell);
        registry.AddSessionGroup("project", group => recorder.PopulateSessionGroup(group, host.Current, []));
    }

    public async ValueTask DisposeAsync()
    {
        shell.Dispose();
        actions.Dispose();
        await editor.DisposeAsync();
    }

    private static (ExtensionManifest, INetPrintsExtension) Entry(string id, INetPrintsExtension extension) =>
        (new ExtensionManifest(id, id, "1.0.0", string.Empty, "1.0", []), extension);

    [Fact]
    public void APanelFactoryResolvesItsExtensionsServiceAndAnotherExtensionsFactoryDoesNot()
    {
        Probe alpha = Assert.IsType<Probe>(shell.FindPanel(Alpha + ".panel")?.Content);
        Probe beta = Assert.IsType<Probe>(shell.FindPanel(Beta + ".panel")?.Content);

        Assert.NotNull(alpha.Service);
        Assert.Null(beta.Service);
    }

    [Fact]
    public void ATileFactoryResolvesItsExtensionsServiceAndAnotherExtensionsFactoryDoesNot()
    {
        var page = new StartPageViewModel(shell, new StartPageContext(actions, PanelFactoryContext.Detached));

        Probe alpha = Assert.IsType<Probe>(Assert.Single(page.Others, tile => tile is Probe { Owner: Alpha }));
        Probe beta = Assert.IsType<Probe>(Assert.Single(page.Others, tile => tile is Probe { Owner: Beta }));

        Assert.NotNull(alpha.Service);
        Assert.Null(beta.Service);
    }

    [Fact]
    public void ABuiltInPanelStillGetsTheEditorsContext()
    {
        Assert.NotNull(shell.FindPanel(PanelContributions.ErrorsId));
    }

    private sealed class AlphaService;

    private sealed record Probe(string Owner, object? Service);

    private sealed class AlphaExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            builder.Services.AddSingleton<AlphaService>();
            builder.Editor
                .AddPanel(new PanelDescriptor(Alpha + ".panel", "Alpha panel", context => new Probe(Alpha, context.Services.GetService<AlphaService>()), PanelDock.Right, 10))
                .AddDashboardTile(new DashboardTileDescriptor(Alpha + ".tile", "Alpha tile", 10, context => new Probe(Alpha, context.Services.GetService<AlphaService>())));
        }
    }

    private sealed class BetaExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) =>
            builder.Editor
                .AddPanel(new PanelDescriptor(Beta + ".panel", "Beta panel", context => new Probe(Beta, context.Services.GetService<AlphaService>()), PanelDock.Right, 20))
                .AddDashboardTile(new DashboardTileDescriptor(Beta + ".tile", "Beta tile", 20, context => new Probe(Beta, context.Services.GetService<AlphaService>())));
    }
}
