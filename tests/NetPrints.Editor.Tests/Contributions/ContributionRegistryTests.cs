using Microsoft.Extensions.Logging;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>contracts/contributions.md §1 and §4: validation, first-wins duplicates and gesture conflicts, freeze, logging.</summary>
public class ContributionRegistryTests
{
    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private readonly CollectingLogger<ContributionRegistry> logger = new();

    private ContributionRegistry NewRegistry() => new(logger);

    private static CommandDescriptor Command(string id, string label = "Label", CommandScope scope = CommandScope.Global, params string[] gestures) =>
        new(id, label, new NoopHandler(), DefaultGestures: gestures, Scope: scope);

    private static PanelDescriptor Panel(string id) => new(id, "Panel", _ => new object(), PanelDock.Left, 0);

    private static DashboardTileDescriptor Tile(string id) => new(id, "Tile", 0, _ => new object());

    private static ProjectTemplateDescriptor Template(string id) => new(id, "Template", "Description", "profile", ProjectOutputType.Console);

    private static ContextMenuItemDescriptor MenuItem(string id) => new(id, ContextMenuTarget.Node, "netprints.command.save", "group", 0);

    private sealed class Tooltips : ITooltipProvider
    {
        public int Order => 0;

        public TooltipContent? TryProvide(TooltipTarget target) => null;
    }

    private sealed class GoTo : IGoToProvider
    {
        public string Kind => "Graphs";

        public async IAsyncEnumerable<GoToItem> SearchAsync(string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    [Theory]
    [InlineData("netprints.command.save")]
    [InlineData("a.b")]
    [InlineData("ext1.Command2.x")]
    public void AValidIdIsAccepted(string id)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command(id));

        Assert.Equal(id, Assert.Single(registry.Commands).Id);
        Assert.Empty(registry.Issues);
    }

    [Theory]
    [InlineData("")]
    [InlineData("save")]
    [InlineData("Netprints.save")]
    [InlineData("netprints..save")]
    [InlineData("netprints.save.")]
    [InlineData("netprints.sa-ve")]
    [InlineData("netprints save")]
    public void ABadIdIsAnInvalidDescriptorAndIsIgnored(string id)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command(id));

        Assert.Empty(registry.Commands);
        var issue = Assert.Single(registry.Issues);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, issue.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyLabelIsAnInvalidDescriptor(string label)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.save", label));

        Assert.Empty(registry.Commands);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
    }

    [Fact]
    public void ASingleKeyGestureInGlobalScopeIsAnInvalidDescriptor()
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.frame", scope: CommandScope.Global, gestures: "F"));

        Assert.Empty(registry.Commands);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
    }

    [Theory]
    [InlineData(CommandScope.Graph)]
    [InlineData(CommandScope.ProjectTree)]
    public void ASingleKeyGestureIsAllowedInANonGlobalScope(CommandScope scope)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.frame", scope: scope, gestures: "F"));

        Assert.Single(registry.Commands);
        Assert.Empty(registry.Issues);
    }

    [Theory]
    [InlineData(CommandScope.Graph | CommandScope.ProjectTree, CommandScope.ProjectTree, true)]
    [InlineData(CommandScope.Graph | CommandScope.ProjectTree, CommandScope.Graph, true)]
    [InlineData(CommandScope.Graph | CommandScope.ProjectTree, CommandScope.Graph | CommandScope.ProjectTree, true)]
    [InlineData(CommandScope.Graph, CommandScope.ProjectTree, false)]
    public void AMultiScopeCommandOverlapsEveryScopeItNames(CommandScope first, CommandScope second, bool conflicts)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.first", scope: first, gestures: "Delete"));
        registry.AddCommand(Command("netprints.command.second", scope: second, gestures: "Delete"));

        Assert.Equal(conflicts ? 1 : 0, registry.Issues.Count(issue => issue.Kind == ContributionIssueKind.GestureConflict));
    }

    [Fact]
    public void AnUnparseableGestureIsAnInvalidDescriptor()
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.save", scope: CommandScope.Graph, gestures: "Ctrl+"));

        Assert.Empty(registry.Commands);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
    }

    [Fact]
    public void ADuplicateIdWithinAKindIsReportedAndTheFirstWins()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.save", "First"));

        registry.AddCommand(Command("netprints.command.save", "Second"));

        Assert.Equal("First", Assert.Single(registry.Commands).Label);
        var issue = Assert.Single(registry.Issues);
        Assert.Equal(ContributionIssueKind.DuplicateId, issue.Kind);
        Assert.Equal("netprints.command.save", issue.Id);
    }

    [Fact]
    public void ADuplicateIdIsReportedForEveryDescriptorKind()
    {
        var registry = NewRegistry();
        registry.AddPanel(Panel("netprints.panel.errors"));
        registry.AddPanel(Panel("netprints.panel.errors"));
        registry.AddDashboardTile(Tile("netprints.tile.recent"));
        registry.AddDashboardTile(Tile("netprints.tile.recent"));
        registry.AddProjectTemplate(Template("netprints.template.console"));
        registry.AddProjectTemplate(Template("netprints.template.console"));
        registry.AddContextMenuItem(MenuItem("netprints.menu.save"));
        registry.AddContextMenuItem(MenuItem("netprints.menu.save"));

        Assert.Single(registry.Panels);
        Assert.Single(registry.DashboardTiles);
        Assert.Single(registry.ProjectTemplates);
        Assert.Single(registry.ContextMenuItems);
        Assert.All(registry.Issues, issue => Assert.Equal(ContributionIssueKind.DuplicateId, issue.Kind));
        Assert.Equal(4, registry.Issues.Count);
    }

    [Fact]
    public void TheSameIdInTwoKindsIsAllowed()
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.shared.id"));
        registry.AddPanel(Panel("netprints.shared.id"));
        registry.AddDashboardTile(Tile("netprints.shared.id"));

        Assert.Single(registry.Commands);
        Assert.Single(registry.Panels);
        Assert.Single(registry.DashboardTiles);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public void OverlappingGesturesInOneScopeConflictAndOnlyTheFirstKeepsIt()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.first", scope: CommandScope.Graph, gestures: "Ctrl+K"));

        registry.AddCommand(Command("netprints.command.second", scope: CommandScope.Graph, gestures: ["Ctrl+K", "Ctrl+L"]));

        Assert.Equal(["Ctrl+K"], registry.Commands[0].DefaultGestures);
        Assert.Equal(["Ctrl+L"], registry.Commands[1].DefaultGestures);
        var issue = Assert.Single(registry.Issues);
        Assert.Equal(ContributionIssueKind.GestureConflict, issue.Kind);
        Assert.Equal("netprints.command.second", issue.Id);
    }

    [Fact]
    public void GesturesCompareCanonically()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.build", scope: CommandScope.Global, gestures: "Ctrl+Shift+B"));

        registry.AddCommand(Command("netprints.command.other", scope: CommandScope.Global, gestures: "shift+ctrl+b"));

        Assert.Empty(registry.Commands[1].DefaultGestures ?? []);
        Assert.Equal(ContributionIssueKind.GestureConflict, Assert.Single(registry.Issues).Kind);
    }

    [Theory]
    [InlineData(CommandScope.Global, CommandScope.Graph)]
    [InlineData(CommandScope.Graph, CommandScope.Global)]
    [InlineData(CommandScope.Global, CommandScope.ProjectTree)]
    [InlineData(CommandScope.ProjectTree, CommandScope.Global)]
    public void GlobalConflictsWithEveryScope(CommandScope first, CommandScope second)
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.first", scope: first, gestures: "Ctrl+K"));

        registry.AddCommand(Command("netprints.command.second", scope: second, gestures: "Ctrl+K"));

        Assert.Empty(registry.Commands[1].DefaultGestures ?? []);
        Assert.Equal(ContributionIssueKind.GestureConflict, Assert.Single(registry.Issues).Kind);
    }

    [Fact]
    public void GraphAndProjectTreeNeverConflict()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.first", scope: CommandScope.Graph, gestures: "Delete"));

        registry.AddCommand(Command("netprints.command.second", scope: CommandScope.ProjectTree, gestures: "Delete"));

        Assert.Equal(["Delete"], registry.Commands[1].DefaultGestures);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public void ListsKeepRegistrationOrder()
    {
        var registry = NewRegistry();
        string[] ids = ["netprints.command.c", "netprints.command.a", "netprints.command.b"];

        foreach (string id in ids)
        {
            registry.AddCommand(Command(id));
        }

        Assert.Equal(ids, registry.Commands.Select(c => c.Id));
    }

    [Fact]
    public void TooltipAndGoToProvidersAreKeptInOrder()
    {
        var registry = NewRegistry();
        var t1 = new Tooltips();
        var t2 = new Tooltips();
        var g1 = new GoTo();

        registry.AddTooltipProvider(t1);
        registry.AddTooltipProvider(t2);
        registry.AddGoToProvider(g1);

        Assert.Equal([t1, t2], registry.TooltipProviders);
        Assert.Same(g1, Assert.Single(registry.GoToProviders));
    }

    [Fact]
    public void FreezeMakesEveryAddThrow()
    {
        var registry = NewRegistry();
        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => registry.AddCommand(Command("netprints.command.save")));
        Assert.Throws<InvalidOperationException>(() => registry.AddPanel(Panel("netprints.panel.errors")));
        Assert.Throws<InvalidOperationException>(() => registry.AddDashboardTile(Tile("netprints.tile.recent")));
        Assert.Throws<InvalidOperationException>(() => registry.AddProjectTemplate(Template("netprints.template.console")));
        Assert.Throws<InvalidOperationException>(() => registry.AddContextMenuItem(MenuItem("netprints.menu.save")));
        Assert.Throws<InvalidOperationException>(() => registry.AddTooltipProvider(new Tooltips()));
        Assert.Throws<InvalidOperationException>(() => registry.AddGoToProvider(new GoTo()));
    }

    [Fact]
    public void ANewRegistryIsNotFrozen() => Assert.False(NewRegistry().IsFrozen);

    [Fact]
    public void EachIssueIsLoggedOnceAtWarningLevel()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("bad"));
        registry.AddCommand(Command("netprints.command.save"));
        registry.AddCommand(Command("netprints.command.save"));

        Assert.Equal(2, registry.Issues.Count);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.Contains("bad", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("netprints.command.save", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIssueRecordsTheOwner()
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("bad"));

        Assert.Equal(ContributionIds.Owner, Assert.Single(registry.Issues).Owner);
    }

    [Fact]
    public void AnEmptyPanelTitleOrTemplateNameIsInvalid()
    {
        var registry = NewRegistry();

        registry.AddPanel(new PanelDescriptor("netprints.panel.errors", "", _ => new object(), PanelDock.Bottom, 0));
        registry.AddProjectTemplate(new ProjectTemplateDescriptor("netprints.template.console", "", "d", "p", ProjectOutputType.Console));

        Assert.Empty(registry.Panels);
        Assert.Empty(registry.ProjectTemplates);
        Assert.All(registry.Issues, issue => Assert.Equal(ContributionIssueKind.InvalidDescriptor, issue.Kind));
        Assert.Equal(2, registry.Issues.Count);
    }
}
