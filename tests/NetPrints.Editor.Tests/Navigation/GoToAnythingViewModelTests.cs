using NetPrints.Editor.Contributions;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class GoToAnythingViewModelTests
{
    private static readonly DocumentId Doc = DocumentId.Graph("A.netpc.json", DocumentId.ClassGraphKey);

    private sealed class FakeProvider(string kind, params GoToItem[] items) : IGoToProvider
    {
        public string Kind { get; } = kind;

        public List<string> Queries { get; } = [];

        public Func<string, CancellationToken, Task>? Gate { get; set; }

        public async IAsyncEnumerable<GoToItem> SearchAsync(string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Queries.Add(text);
            if (Gate is not null)
            {
                await Gate(text, cancellationToken);
            }

            foreach (GoToItem item in items)
            {
                yield return item;
            }
        }
    }

    private readonly List<NavigationTarget> navigated = [];
    private readonly List<string> ran = [];
    private bool navigateResult = true;

    private static GoToItem Item(string kind, string title, string detail = "") => new(kind, title, detail, new NavigationTarget(Doc, title));

    private GoToAnythingViewModel Create(params IGoToProvider[] providers) =>
        new(providers, target =>
        {
            navigated.Add(target);
            return navigateResult;
        }, id =>
        {
            ran.Add(id);
            return true;
        });

    private static async Task SearchAsync(GoToAnythingViewModel vm, string text)
    {
        vm.Query = text;
        await vm.SearchCommand.ExecutionTask!;
    }

    [Fact]
    public async Task ResultsAreGroupedByKindInProviderOrderAndRankedWithinEachGroup()
    {
        var graphs = new FakeProvider("Graphs", Item("Graphs", "Resave"), Item("Graphs", "Save b"), Item("Graphs", "Undo save"));
        var nodes = new FakeProvider("Nodes", Item("Nodes", "Save a"));
        var vm = Create(graphs, nodes);

        await SearchAsync(vm, "sa");

        Assert.Equal(
            [("Graphs", true), ("Save b", false), ("Undo save", false), ("Resave", false), ("Nodes", true), ("Save a", false)],
            vm.Rows.Select(row => (row.Title, row.IsHeader)));
        Assert.Equal("Save b", vm.SelectedRow?.Title);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task ALeadingGreaterThanSearchesOnlyTheCommandsProviderWithTheRestOfTheText()
    {
        var graphs = new FakeProvider("Graphs", Item("Graphs", "Save graph"));
        var commands = new FakeProvider(GoToKinds.Commands, new GoToItem(GoToKinds.Commands, "Save", "File", null, "netprints.command.save"));
        var vm = Create(graphs, commands);

        await SearchAsync(vm, "> sa");

        Assert.Empty(graphs.Queries.Where(query => query.Length > 0));
        Assert.Equal("sa", commands.Queries[^1]);
        Assert.Equal(["Commands", "Save"], vm.Rows.Select(row => row.Title));
    }

    [Fact]
    public async Task WithoutTheMarkerTheCommandsProviderIsNotAsked()
    {
        var commands = new FakeProvider(GoToKinds.Commands, new GoToItem(GoToKinds.Commands, "Save", "", null, "netprints.command.save"));
        var vm = Create(new FakeProvider("Graphs"), commands);

        await SearchAsync(vm, "sa");

        Assert.DoesNotContain("sa", commands.Queries);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task EnterNavigatesToTheSelectedItemsTargetAndCloses()
    {
        var vm = Create(new FakeProvider("Nodes", Item("Nodes", "Alpha"), Item("Nodes", "Alpine")));
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        await SearchAsync(vm, "al");

        vm.SelectNextCommand.Execute(null);
        vm.RunSelectedCommand.Execute(null);

        Assert.Equal(new NavigationTarget(Doc, "Alpine"), Assert.Single(navigated));
        Assert.True(closed);
    }

    [Fact]
    public async Task ACommandItemRunsItsCommandAndANavigationThatFailsKeepsTheDialogOpen()
    {
        var commands = new FakeProvider(GoToKinds.Commands, new GoToItem(GoToKinds.Commands, "Save", "", null, "netprints.command.save"));
        var vm = Create(commands);
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        await SearchAsync(vm, ">sa");

        vm.RunSelectedCommand.Execute(null);

        Assert.Equal(["netprints.command.save"], ran);
        Assert.True(closed);

        var other = Create(new FakeProvider("Nodes", Item("Nodes", "Alpha")));
        navigateResult = false;
        bool otherClosed = false;
        other.CloseRequested += (_, _) => otherClosed = true;
        await SearchAsync(other, "al");
        other.RunSelectedCommand.Execute(null);

        Assert.False(otherClosed);
    }

    [Fact]
    public async Task AHeaderIsNeverSelectedAndArrowsSkipHeaders()
    {
        var vm = Create(new FakeProvider("A", Item("A", "one")), new FakeProvider("B", Item("B", "one b")));
        await SearchAsync(vm, "one");

        vm.SelectedRow = vm.Rows[0];
        Assert.False(vm.SelectedRow?.IsHeader);
        vm.SelectNextCommand.Execute(null);
        Assert.Equal("one b", vm.SelectedRow?.Title);
        vm.SelectNextCommand.Execute(null);
        Assert.Equal("one b", vm.SelectedRow?.Title);
        vm.SelectPreviousCommand.Execute(null);
        Assert.Equal("one", vm.SelectedRow?.Title);
        vm.SelectPreviousCommand.Execute(null);
        Assert.Equal("one", vm.SelectedRow?.Title);
    }

    [Fact]
    public async Task ASlowSearchIsCancelledWhenTheTextChanges()
    {
        var provider = new FakeProvider("Nodes", Item("Nodes", "fast"))
        {
            Gate = (text, cancellationToken) => text == "slow" ? Task.Delay(Timeout.Infinite, cancellationToken) : Task.CompletedTask,
        };
        var vm = Create(provider);

        vm.Query = "slow";
        Task slow = vm.SearchCommand.ExecutionTask!;
        await SearchAsync(vm, "fast");
        await slow;

        Assert.Equal(["Nodes", "fast"], vm.Rows.Select(row => row.Title));
    }

    [Fact]
    public async Task EscapeClosesWithoutNavigating()
    {
        var vm = Create(new FakeProvider("Nodes", Item("Nodes", "Alpha")));
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        await SearchAsync(vm, "al");

        vm.CloseCommand.Execute(null);

        Assert.True(closed);
        Assert.Empty(navigated);
    }
}
