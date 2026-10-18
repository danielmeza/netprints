using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Settings;

/// <summary>FR-046, FR-048-FR-050, research R21: the effective gestures of every command.</summary>
public sealed class KeyBindingResolverTests
{
    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CommandDescriptor Command(string id, CommandScope scope = CommandScope.Global, params string[] gestures) =>
        new(id, id, new NoopHandler(), DefaultGestures: [.. gestures.Select(text => CommandGesture.Parse(text, null))], Scope: scope);

    private static KeyboardSettings Settings(params KeyBindingOverride[] entries) => new() { Bindings = entries };

    private static KeyBindingOverride Override(string command, string[]? add = null, string[]? remove = null, string scope = "Global") =>
        new() { Command = command, Scope = scope, Add = add ?? [], Remove = remove ?? [] };

    private static string[] Texts(IEnumerable<CommandGesture> gestures) => [.. gestures.Select(gesture => gesture.ToString())];

    [Fact]
    public void TheEffectiveSetIsTheDefaultsWithoutTheRemovedPlusTheAdded()
    {
        CommandDescriptor[] commands = [Command("a.one", CommandScope.Global, "Ctrl+1", "Ctrl+2")];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.one", add: ["Ctrl+3"], remove: ["Ctrl+1"])));

        CommandBindings row = result.For("a.one");
        Assert.Equal(["Ctrl+1", "Ctrl+2"], Texts(row.Defaults));
        Assert.Equal(["Ctrl+2", "Ctrl+3"], Texts(row.Effective));
        Assert.True(row.IsChanged);
    }

    [Fact]
    public void ACommandWithoutAnEntryKeepsItsDefaults()
    {
        CommandDescriptor[] commands = [Command("a.one", CommandScope.Global, "Ctrl+1")];

        CommandBindings row = KeyBindingResolver.Resolve(commands, new KeyboardSettings()).For("a.one");

        Assert.Equal(["Ctrl+1"], Texts(row.Effective));
        Assert.False(row.IsChanged);
        Assert.Empty(row.Overridden);
    }

    [Fact]
    public void AUserBindingBeatsTheBuiltInDefaultAndMarksItOverridden()
    {
        CommandDescriptor[] commands = [Command("a.builtin", CommandScope.Global, "Ctrl+K"), Command("b.other")];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("b.other", add: ["Ctrl+K"])));

        Assert.Equal(["Ctrl+K"], Texts(result.For("b.other").Effective));
        Assert.Empty(result.For("a.builtin").Effective);
        Assert.Equal(["Ctrl+K"], Texts(result.For("a.builtin").Overridden));
    }

    [Fact]
    public void AnExtensionDefaultThatAUserBindingTakesIsMarkedOverridden()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(Command("netprints.command.mine", CommandScope.Global));
        registry.For("ext.one", "Extension One").AddCommand(Command("ext.one.run", CommandScope.Global, "Ctrl+Alt+R"));
        registry.SetUserBindings(Settings(Override("netprints.command.mine", add: ["Ctrl+Alt+R"])));

        EffectiveKeyBindings result = registry.Snapshot.KeyBindings;

        Assert.Equal(["Ctrl+Alt+R"], Texts(result.For("ext.one.run").Overridden));
        Assert.Empty(registry.Snapshot.Commands.Single(command => command.Id == "ext.one.run").DefaultGestures ?? []);
        Assert.Equal(["Ctrl+Alt+R"], Texts(registry.Snapshot.Commands.Single(command => command.Id == "netprints.command.mine").DefaultGestures ?? []));
    }

    [Fact]
    public void ABuiltInDefaultComesBeforeAnExtensionDefault()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(Command("netprints.command.mine", CommandScope.Global, "Ctrl+Alt+R"));
        registry.For("ext.one").AddCommand(Command("ext.one.run", CommandScope.Global, "Ctrl+Alt+R"));

        EffectiveKeyBindings result = registry.Snapshot.KeyBindings;

        Assert.Equal(["Ctrl+Alt+R"], Texts(result.For("netprints.command.mine").Effective));
        Assert.Empty(result.For("ext.one.run").Effective);
    }

    [Theory]
    [InlineData("K")]
    [InlineData("Shift+K")]
    [InlineData("Delete")]
    public void ASingleKeyOrShiftOnlyGestureInGlobalIsRefusedWithTheReason(string gesture)
    {
        CommandDescriptor[] commands = [Command("a.one", CommandScope.Global)];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.one", add: [gesture])));

        Assert.Empty(result.For("a.one").Effective);
        KeyBindingIssue issue = Assert.Single(result.Issues);
        Assert.Equal("a.one", issue.Command);
        Assert.Equal(gesture, issue.Gesture);
        Assert.Contains("Global", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionKeysAreExemptFromTheGlobalScopeRule()
    {
        CommandDescriptor[] commands = [Command("a.one", CommandScope.Global)];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.one", add: ["F7", "Shift+F8"])));

        Assert.Equal(["F7", "Shift+F8"], Texts(result.For("a.one").Effective));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ASingleKeyGestureIsAllowedInANarrowScope()
    {
        CommandDescriptor[] commands = [Command("a.graph", CommandScope.Graph)];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.graph", add: ["Delete"], scope: "Graph")));

        Assert.Equal(["Delete"], Texts(result.For("a.graph").Effective));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void AnOverrideOfACommandThatIsNotLoadedIsKept()
    {
        CommandDescriptor[] commands = [Command("a.one")];
        KeyBindingOverride gone = Override("ext.gone.run", add: ["Ctrl+Alt+G"], scope: "Graph");

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(gone));

        Assert.Equal(gone, Assert.Single(result.NotLoaded));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void AnUnparseableGestureIsReportedAndIgnored()
    {
        CommandDescriptor[] commands = [Command("a.one", CommandScope.Global, "Ctrl+1")];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.one", add: ["Ctrl+Nope", "Ctrl+2"], remove: ["???"])));

        Assert.Equal(["Ctrl+1", "Ctrl+2"], Texts(result.For("a.one").Effective));
        Assert.Equal(["???", "Ctrl+Nope"], result.Issues.Select(issue => issue.Gesture));
    }

    [Fact]
    public void TwoUserBindingsOfTheSameGestureKeepTheFirst()
    {
        CommandDescriptor[] commands = [Command("a.one"), Command("a.two")];

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(commands, Settings(Override("a.one", add: ["Ctrl+9"]), Override("a.two", add: ["Ctrl+9"])));

        Assert.Equal(["Ctrl+9"], Texts(result.For("a.one").Effective));
        Assert.Empty(result.For("a.two").Effective);
        Assert.Equal("a.two", Assert.Single(result.Issues).Command);
    }

    [Fact]
    public void TheSetIsComputedOnceForASnapshotAndAgainForTheNext()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(Command("netprints.command.mine", CommandScope.Global, "Ctrl+1"));
        ContributionSnapshot first = registry.Snapshot;

        Assert.Same(first.KeyBindings, registry.Snapshot.KeyBindings);

        registry.SetUserBindings(Settings(Override("netprints.command.mine", add: ["Ctrl+2"])));

        Assert.NotSame(first.KeyBindings, registry.Snapshot.KeyBindings);
        Assert.Equal(["Ctrl+1", "Ctrl+2"], Texts(registry.Snapshot.Commands.Single().DefaultGestures ?? []));
    }

    [Fact]
    public void ChangingTheBindingsRaisesChangedSoEverySurfaceRebuilds()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(Command("netprints.command.mine", CommandScope.Global, "Ctrl+1"));
        registry.Freeze();
        int raised = 0;
        registry.Changed += (_, _) => raised++;

        registry.SetUserBindings(Settings(Override("netprints.command.mine", remove: ["Ctrl+1"])));

        Assert.Equal(1, raised);
        Assert.Empty(registry.Commands.Single().DefaultGestures ?? []);
    }
}
