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
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private readonly CollectingLogger<ContributionRegistry> logger = new();

    private ContributionRegistry NewRegistry() => new(logger);

    private static CommandDescriptor Command(string id, string label = "Label", CommandScope scope = CommandScope.Global, params string[] gestures) =>
        new(id, label, new NoopHandler(), DefaultGestures: [.. gestures.Select(text => CommandGesture.Parse(text, null))], Scope: scope);

    private static string[] Texts(CommandDescriptor command) => [.. (command.DefaultGestures ?? []).Select(gesture => gesture.ToString())];

    private static PanelDescriptor Panel(string id) => new(id, "Panel", _ => new object(), PanelDock.Left, 0);

    private static DashboardTileDescriptor Tile(string id) => new(id, "Tile", 0, _ => new object());

    private static ProjectTemplateDescriptor Template(string id) => new(id, "Template", "Description", "profile", ProjectOutputType.Console);

    private static ContextMenuItemDescriptor MenuItem(string id) => new(id, ContextMenuTarget.Connection, "netprints.command.save", "group", 0);

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
    [InlineData("Shift+A")]
    [InlineData("Shift+3")]
    [InlineData("Shift+Home")]
    public void AShiftOnlyGestureInGlobalScopeIsAnInvalidDescriptor(string gesture)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.caps", scope: CommandScope.Global, gestures: gesture));

        Assert.Empty(registry.Commands);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
    }

    [Fact]
    public void AShiftFunctionKeyIsAllowedInGlobalScope()
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.stop", scope: CommandScope.Global, gestures: "Shift+F5"));

        Assert.Single(registry.Commands);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public void ANullGestureIsAnInvalidDescriptor()
    {
        var registry = NewRegistry();
        CommandDescriptor command = Command("netprints.command.odd") with { DefaultGestures = new CommandGesture[1] };

        registry.AddCommand(command);

        Assert.Empty(registry.Commands);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
    }

    [Theory]
    [InlineData("Esc", "Escape")]
    [InlineData("Del", "Delete")]
    [InlineData("Return", "Enter")]
    public void AnAliasOfAKeyConflictsWithItsCanonicalName(string alias, string canonical)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.first", scope: CommandScope.Graph, gestures: canonical));
        registry.AddCommand(Command("netprints.command.second", scope: CommandScope.Graph, gestures: alias));

        Assert.Equal(ContributionIssueKind.GestureConflict, Assert.Single(registry.Issues).Kind);
        Assert.Empty(registry.Commands.Single(command => command.Id == "netprints.command.second").DefaultGestures ?? []);
    }

    [Theory]
    [InlineData("F5")]
    [InlineData("F12")]
    public void AFunctionKeyIsAllowedInGlobalScope(string gesture)
    {
        var registry = NewRegistry();

        registry.AddCommand(Command("netprints.command.run", scope: CommandScope.Global, gestures: gesture));

        Assert.Single(registry.Commands);
        Assert.Empty(registry.Issues);
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
    public void ADuplicateIdWithinAKindIsReportedAndTheFirstWins()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.save", "First"));

        registry.AddCommand(Command("netprints.command.save", "Second"));

        Assert.Equal("First", Assert.Single(registry.Commands).Label.Text);
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

        Assert.Equal(["Ctrl+K"], Texts(registry.Commands[0]));
        Assert.Equal(["Ctrl+L"], Texts(registry.Commands[1]));
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

        Assert.Equal(["Delete"], Texts(registry.Commands[1]));
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

    private const string AppExtension = "app.ext";
    private const string SessionExtension = "session.ext";
    private const string SessionKey = "project-1";

    private static void Fill(IContributionGroupBuilder group, string owner = SessionExtension, string? name = "Session Extension", string prefix = "session.ext")
    {
        IEditorContributions editor = group.For(owner, name);
        editor.AddCommand(Command(prefix + ".hello", "Hello"));
        editor.AddDashboardTile(Tile(prefix + ".tile"));
        editor.AddContextMenuItem(MenuItem(prefix + ".menu"));
        editor.AddTooltipProvider(new Tooltips());
        editor.AddGoToProvider(new GoTo());
        group.AddProjectTemplate(Template(prefix + ".template"));
    }

    [Fact]
    public void ASessionGroupFollowsTheAppGroupInEveryKind()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("app.ext.first"));
        registry.AddPanel(Panel("app.ext.panel"));

        registry.AddSessionGroup(SessionKey, group =>
        {
            Fill(group);
            group.For(SessionExtension).AddPanel(Panel("session.ext.panel"));
        });

        Assert.Equal(["app.ext.first", "session.ext.hello"], registry.Commands.Select(c => c.Id).Where(id => !id.Contains("showPanel", StringComparison.Ordinal)));
        Assert.Equal(["app.ext.panel", "session.ext.panel"], registry.Panels.Select(p => p.Id));
        Assert.Equal("session.ext.tile", Assert.Single(registry.DashboardTiles).Id);
        Assert.Equal("session.ext.menu", Assert.Single(registry.ContextMenuItems).Id);
        Assert.Equal("session.ext.template", Assert.Single(registry.ProjectTemplates).Id);
        Assert.Single(registry.TooltipProviders);
        Assert.Single(registry.GoToProviders);
        Assert.Equal(SessionExtension, registry.OwnerOf("session.ext.hello"));
    }

    [Fact]
    public void ASessionGroupIsRemovedAsAWhole()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("app.ext.first"));
        registry.AddSessionGroup(SessionKey, group => Fill(group));

        Assert.True(registry.RemoveSessionGroup(SessionKey));

        Assert.Equal("app.ext.first", Assert.Single(registry.Commands).Id);
        Assert.Empty(registry.Panels);
        Assert.Empty(registry.DashboardTiles);
        Assert.Empty(registry.ContextMenuItems);
        Assert.Empty(registry.ProjectTemplates);
        Assert.Empty(registry.TooltipProviders);
        Assert.Empty(registry.GoToProviders);
        Assert.Null(registry.OwnerOf("session.ext.hello"));
        Assert.False(registry.RemoveSessionGroup(SessionKey));
    }

    [Fact]
    public void EveryChangeRaisesChangedWithANewImmutableSnapshot()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("app.ext.first"));
        registry.Freeze();
        var seen = new List<ContributionSnapshot>();
        registry.Changed += (_, snapshot) => seen.Add(snapshot);

        registry.AddSessionGroup(SessionKey, group => Fill(group));
        registry.RemoveSessionGroup(SessionKey);

        Assert.Equal(2, seen.Count);
        Assert.NotSame(seen[0], seen[1]);
        Assert.Equal(["app.ext.first", "session.ext.hello"], seen[0].Commands.Select(c => c.Id));
        Assert.Equal(["app.ext.first"], seen[1].Commands.Select(c => c.Id));
        Assert.Same(seen[1], registry.Snapshot);
    }

    [Fact]
    public void ASessionGroupIsPublishedOnceAsAWhole()
    {
        var registry = NewRegistry();
        int raised = 0;
        registry.Changed += (_, _) => raised++;

        registry.AddSessionGroup(SessionKey, group => Fill(group));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void AnAppAddAfterFreezeRaisesChanged()
    {
        var registry = NewRegistry();
        registry.Freeze();
        ContributionSnapshot? seen = null;
        registry.Changed += (_, snapshot) => seen = snapshot;

        registry.AddCommand(Command("app.ext.late"));

        Assert.Equal("app.ext.late", Assert.Single(Assert.IsType<ContributionSnapshot>(seen).Commands).Id);
    }

    [Fact]
    public void AnOldSnapshotDoesNotChange()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("app.ext.first"));
        ContributionSnapshot before = registry.Snapshot;

        registry.AddSessionGroup(SessionKey, group => Fill(group));

        Assert.Equal("app.ext.first", Assert.Single(before.Commands).Id);
        Assert.Equal(2, registry.Snapshot.Commands.Count);
    }

    [Fact]
    public void WithinAKindTheFirstRegistrationWinsAcrossGroups()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.save", "Built-in"));

        registry.AddSessionGroup(SessionKey, group => group.For(SessionExtension).AddCommand(Command("netprints.command.save", "Extension")));

        Assert.Equal("Built-in", Assert.Single(registry.Commands).Label.Text);
        Assert.Contains(registry.Issues, issue => issue.Kind == ContributionIssueKind.DuplicateId && issue.Owner == SessionExtension);
    }

    [Fact]
    public void AGestureConflictAcrossGroupsKeepsTheFirstBinding()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("app.ext.first", "First", CommandScope.Global, "Ctrl+K"));

        registry.AddSessionGroup(SessionKey, group => group.For(SessionExtension).AddCommand(Command("session.ext.second", "Second", CommandScope.Global, "Ctrl+K")));

        Assert.Empty(Texts(registry.Commands.Single(c => c.Id == "session.ext.second")));
        Assert.Contains(registry.Issues, issue => issue.Kind == ContributionIssueKind.GestureConflict);
    }

    [Fact]
    public void ASecondSessionGroupDoesNotReplaceTheFirstGroupsIds()
    {
        var registry = NewRegistry();
        registry.AddSessionGroup("one", group => Fill(group));

        registry.AddSessionGroup("two", group => Fill(group, "other.ext", "Other", "session.ext"));
        registry.RemoveSessionGroup("two");

        Assert.Equal(SessionExtension, registry.OwnerOf("session.ext.hello"));
        Assert.Equal("session.ext.hello", Assert.Single(registry.Commands).Id);
    }

    [Fact]
    public void AGroupThatThrowsWhileFillingPublishesNothing()
    {
        var registry = NewRegistry();
        int raised = 0;
        registry.Changed += (_, _) => raised++;

        Assert.Throws<InvalidOperationException>(() => registry.AddSessionGroup(SessionKey, group =>
        {
            group.For(SessionExtension).AddCommand(Command("session.ext.hello"));
            throw new InvalidOperationException("boom");
        }));

        Assert.Empty(registry.Commands);
        Assert.Equal(0, raised);
        registry.AddSessionGroup(SessionKey, group => Fill(group));
        Assert.Single(registry.Commands);
    }

    [Fact]
    public void AnInUseSessionKeyThrows()
    {
        var registry = NewRegistry();
        registry.AddSessionGroup(SessionKey, group => Fill(group));

        Assert.Throws<InvalidOperationException>(() => registry.AddSessionGroup(SessionKey, group => Fill(group, prefix: "other.ext")));
    }

    [Fact]
    public void TheOwnerNameIsTheExtensionNameTheIdOrNothingForBuiltIns()
    {
        var registry = NewRegistry();
        registry.AddCommand(Command("netprints.command.save"));
        registry.For(AppExtension).AddCommand(Command("app.ext.nameless"));
        registry.AddSessionGroup(SessionKey, group => Fill(group));

        Assert.Null(registry.OwnerNameOf("netprints.command.save"));
        Assert.Equal(AppExtension, registry.OwnerNameOf("app.ext.nameless"));
        Assert.Equal("Session Extension", registry.OwnerNameOf("session.ext.hello"));
        Assert.Null(registry.OwnerNameOf("no.such.id"));
    }

    [Fact]
    public void FreezeAddsAShowPanelCommandForASessionPanelToo()
    {
        var registry = NewRegistry();
        registry.Freeze();

        registry.AddSessionGroup(SessionKey, group => group.For(SessionExtension).AddPanel(Panel("session.ext.panel")));

        Assert.Contains(registry.Commands, command => command.Id == ContributionIds.CommandPrefix + "showPanel.session.ext.panel");
        registry.RemoveSessionGroup(SessionKey);
        Assert.Empty(registry.Commands);
    }

    [Fact]
    public void FreezeDoesNotStopLaterAdds()
    {
        var registry = NewRegistry();
        registry.Freeze();

        registry.AddCommand(Command("app.ext.late"));
        registry.AddPanel(Panel("app.ext.panel"));
        registry.AddProjectTemplate(Template("app.ext.template"));

        Assert.Contains(registry.Commands, command => command.Id == "app.ext.late");
        Assert.Contains(registry.Commands, command => command.Id == ContributionIds.CommandPrefix + "showPanel.app.ext.panel");
        Assert.Single(registry.Panels);
        Assert.Single(registry.ProjectTemplates);
    }

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
    public void ACommandWithoutAHandlerIsInvalid() => AssertInvalid(registry => registry.AddCommand(InvalidDescriptors.CommandWithoutHandler()));

    [Fact]
    public void APanelWithoutAViewModelFactoryIsInvalid() => AssertInvalid(registry => registry.AddPanel(InvalidDescriptors.PanelWithoutFactory()));

    [Fact]
    public void ATileWithoutAViewModelFactoryIsInvalid() => AssertInvalid(registry => registry.AddDashboardTile(InvalidDescriptors.TileWithoutFactory()));

    [Fact]
    public void ATileWithABlankTitleIsInvalid() =>
        AssertInvalid(registry => registry.AddDashboardTile(new DashboardTileDescriptor("netprints.tile.recent", " ", 0, _ => new object())));

    [Fact]
    public void ATemplateWithABlankProfileIdIsInvalid() =>
        AssertInvalid(registry => registry.AddProjectTemplate(new ProjectTemplateDescriptor("netprints.template.console", "Console", "d", "", ProjectOutputType.Console)));

    [Fact]
    public void AMenuItemWithAnInvalidCommandIdIsInvalid() =>
        AssertInvalid(registry => registry.AddContextMenuItem(new ContextMenuItemDescriptor("netprints.menu.save", ContextMenuTarget.Connection, "not a command", "group", 0)));

    [Theory]
    [InlineData(ContextMenuTarget.Node)]
    [InlineData(ContextMenuTarget.Pin)]
    [InlineData(ContextMenuTarget.Canvas)]
    public void AMenuItemForATargetWithoutASurfaceIsReported(ContextMenuTarget target) =>
        AssertInvalid(registry => registry.AddContextMenuItem(new ContextMenuItemDescriptor("testext.menu.hello", target, "netprints.command.save", "group", 0)));

    [Theory]
    [InlineData(ContextMenuTarget.Connection)]
    [InlineData(ContextMenuTarget.TreeClass)]
    [InlineData(ContextMenuTarget.TreeMember)]
    [InlineData(ContextMenuTarget.TreeEventGraph)]
    public void AMenuItemForATargetWithASurfaceIsAccepted(ContextMenuTarget target)
    {
        var registry = NewRegistry();

        registry.AddContextMenuItem(new ContextMenuItemDescriptor("testext.menu.hello", target, "netprints.command.save", "group", 0));

        Assert.Empty(registry.Issues);
        Assert.Single(registry.ContextMenuItems);
    }

    [Fact]
    public void EveryTargetTheRegistryAcceptsIsConsumedByAView()
    {
        string source = Path.Combine(FindRepositoryRoot(), "src", "NetPrints.Editor");
        string consumers = string.Concat(Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Contributions" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(path => File.ReadAllText(path)));

        foreach (ContextMenuTarget target in Enum.GetValues<ContextMenuTarget>().Where(ContextMenuTargets.HasSurface))
        {
            Assert.Contains("ContextMenuTarget." + target, consumers, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NetPrints.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root was not found.");
    }

    [Fact]
    public void AMenuItemWithANullGroupIsInvalid() => AssertInvalid(registry => registry.AddContextMenuItem(InvalidDescriptors.MenuItemWithoutGroup()));

    private void AssertInvalid(Action<ContributionRegistry> add)
    {
        var registry = NewRegistry();

        add(registry);

        Assert.Empty(registry.Commands);
        Assert.Empty(registry.Panels);
        Assert.Empty(registry.DashboardTiles);
        Assert.Empty(registry.ProjectTemplates);
        Assert.Empty(registry.ContextMenuItems);
        Assert.Equal(ContributionIssueKind.InvalidDescriptor, Assert.Single(registry.Issues).Kind);
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
