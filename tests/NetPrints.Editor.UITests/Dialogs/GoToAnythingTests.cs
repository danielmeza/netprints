using Avalonia.Headless.XUnit;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The go to anything window: opening, typing, grouping, Enter and Esc (FR-061).</summary>
public class GoToAnythingTests
{
    private static readonly DocumentId Doc = DocumentId.Graph("A.netpc.json", DocumentId.ClassGraphKey);

    private sealed class Provider(string kind, params string[] titles) : IGoToProvider
    {
        public string Kind { get; } = kind;

        public IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken) =>
            titles.Select(title => new GoToItem(Kind, title, "Class", new NavigationTarget(Doc, title))).ToAsyncEnumerable();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GoToAnythingViewModel Create(List<NavigationTarget> navigated) =>
        new([new Provider("Methods", "Save", "Load"), new Provider("Nodes", "Save node")], target =>
        {
            navigated.Add(target);
            return true;
        }, _ => true);

    private static string[] Names(HeadlessUi ui, string id) =>
        [.. ui.Tree.FindControls(new AutomationQuery(id)).Select(pair => Avalonia.Automation.AutomationProperties.GetName(pair.Control) ?? "")];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TypingListsTheResultsUnderTheirKindHeaders()
    {
        List<NavigationTarget> navigated = [];
        using var ui = HeadlessUi.Create();
        ui.Show(new GoToAnythingDialog(Create(navigated)));

        await ui.Driver.TypeAsync("sa", Token);
        await Task.Yield();

        Assert.Contains("Methods", Names(ui, AutomationIds.GoToHeader));
        Assert.Contains("Nodes", Names(ui, AutomationIds.GoToHeader));
        Assert.Equal(["Save", "Save node"], Names(ui, AutomationIds.GoToRow).Where(name => name.StartsWith("Save", StringComparison.Ordinal)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterNavigatesToTheSelectedResultAndCloses()
    {
        List<NavigationTarget> navigated = [];
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new GoToAnythingDialog(Create(navigated)));

        await ui.Driver.TypeAsync("lo", Token);
        await Task.Yield();
        await ui.Driver.PressAsync("Enter", Token);

        Assert.Equal(new NavigationTarget(Doc, "Load"), Assert.Single(navigated));
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EscapeClosesWithoutNavigating()
    {
        List<NavigationTarget> navigated = [];
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new GoToAnythingDialog(Create(navigated)));

        await ui.Driver.TypeAsync("sa", Token);
        await ui.Driver.PressAsync("Escape", Token);

        Assert.False(dialog.IsVisible);
        Assert.Empty(navigated);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NothingMatchingShowsTheEmptyState()
    {
        List<NavigationTarget> navigated = [];
        using var ui = HeadlessUi.Create();
        ui.Show(new GoToAnythingDialog(new GoToAnythingViewModel([], _ => true, _ => true)));

        await ui.Driver.TypeAsync("zzz", Token);

        Assert.Single(ui.Tree.FindControls(new AutomationQuery(AutomationIds.GoToEmpty)));
        Assert.Empty(navigated);
    }
}
