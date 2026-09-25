using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Search;

/// <summary>Component object of the node search popup (PAR-52..54).</summary>
public sealed class SearchPopup(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.GraphSearchPopup) { Within = window, IncludeHidden = true })
{
    public UiElement View => new(Driver, new AutomationQuery(AutomationIds.NodeSearch) { Within = Query });
    public UiElement SearchBox => new(Driver, new AutomationQuery(AutomationIds.SearchBox) { Within = Query });
    public UiElement Results => new(Driver, new AutomationQuery(AutomationIds.SearchResults) { Within = Query });

    public UiElement Row(string text) => Results.Find(AutomationIds.SearchRowText, text: text, index: 0);

    /// <summary>The 16-px icon of a row.</summary>
    public UiElement RowIcon(string text) => Results.Find(AutomationIds.SearchRowIcon, name: text, index: 0);

    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken) => await PropertyAsync(AutomationPropertyNames.IsOpen, cancellationToken) == "True";

    /// <summary>Waits until the popup is open with its suggestions listed.</summary>
    public async Task<SearchPopup> WaitOpenAsync(CancellationToken cancellationToken)
    {
        await WaitUntilAsync(e => e[AutomationPropertyNames.IsOpen] == "True", "open", cancellationToken);
        await Results.WaitUntilAsync(e => int.TryParse(e[AutomationPropertyNames.ItemCount], out int n) && n > 0, "suggestions listed", cancellationToken, TimeSpan.FromSeconds(60));
        return this;
    }

    public Task WaitClosedAsync(CancellationToken cancellationToken) => WaitUntilAsync(e => e[AutomationPropertyNames.IsOpen] == "False", "closed", cancellationToken);

    public async Task<IReadOnlyList<string>> RowTextsAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.SearchRowText) { Within = Results.Query }, cancellationToken))
            .Select(e => e.Text ?? "").ToList();

    /// <summary>Types a filter and waits until a row with <paramref name="expectedRow"/> is shown.</summary>
    public async Task<SearchPopup> FilterAsync(string text, string expectedRow, CancellationToken cancellationToken)
    {
        await Driver.TypeAsync(text, cancellationToken);
        await Row(expectedRow).GetAsync(cancellationToken);
        return this;
    }

    /// <summary>Types a filter and waits until a row satisfying <paramref name="expectedRow"/> is shown; returns that row's text.</summary>
    public async Task<string> FilterAsync(string text, Func<string, bool> expectedRow, CancellationToken cancellationToken)
    {
        await Driver.TypeAsync(text, cancellationToken);
        var rows = await UiWait.ForAsync(Driver, () => RowTextsAsync(cancellationToken), r => r.Any(expectedRow), $"a matching row after typing '{text}'",
            cancellationToken, TimeSpan.FromSeconds(60));
        return rows.First(expectedRow);
    }

    public Task ChooseAsync(string rowText, CancellationToken cancellationToken) => Row(rowText).ClickAsync(cancellationToken);

    public Task PressEnterAsync(CancellationToken cancellationToken) => Driver.PressAsync("Enter", cancellationToken);
}
