using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using NetPrints.Editor.Search;

namespace NetPrints.Editor.UITests.Search;

/// <summary>Page object of the node search popup.</summary>
public sealed class NodeSearchPage(Window window, Popup popup, SuggestionListVM viewModel)
{
    public SuggestionListVM ViewModel { get; } = viewModel;
    private Control Root => (Control)popup.Child!;
    public NodeSearchView View => (NodeSearchView)Root;
    public TextBox SearchBox => Root.ById<TextBox>(AutomationIds.SearchBox);
    public ListBox Results => Root.ById<ListBox>(AutomationIds.SearchResults);
    public int RealizedRowCount => Results.GetRealizedContainers().Count();

    /// <summary>Waits until the popup is open and its suggestions are built.</summary>
    public Task WaitReadyAsync() =>
        HeadlessInput.WaitUntilAsync(() => popup.IsOpen && ViewModel.IsOpen && !ViewModel.IsLoading && ViewModel.Items.Count > 0,
            "search open with suggestions", 60_000);

    public Task WaitClosedAsync() => HeadlessInput.WaitUntilAsync(() => !ViewModel.IsOpen, "search closed");

    public void Type(string text) => window.Type(text);

    public void PressEnter() => window.Press(Key.Enter);

    /// <summary>Waits until the visible rows are the ones matching a filter.</summary>
    public Task WaitFilteredAsync(Func<SuggestionItem, bool> expectation, string what) =>
        HeadlessInput.WaitUntilAsync(() => ViewModel.Items.Any(i => !i.IsHeader) && ViewModel.Items.Where(i => !i.IsHeader).All(expectation), what);

    public TextBlock Row(string text) => Results.AllById<TextBlock>(AutomationIds.SearchRowText).First(t => t.Text == text);

    /// <summary>The 16-px icon shown next to a row.</summary>
    public Image RowIcon(string text) => ((Avalonia.Visual)Row(text).Parent!).Descendants<Image>().Single();

    public void ClickRow(string text) => window.Click(Row(text).CenterIn(window));
}
