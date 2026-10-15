using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Dialogs;

/// <summary>Screen object of the command palette window.</summary>
public sealed class CommandPalettePage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.PaletteDialog))
{
    public UiElement QueryBox => Find(AutomationIds.PaletteQuery);

    /// <summary>A row by the command's label.</summary>
    public UiElement Row(string label) => Find(AutomationIds.PaletteRow, name: label);
}

/// <summary>Screen object of the go to anything window.</summary>
public sealed class GoToAnythingPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.GoToDialog))
{
    public UiElement QueryBox => Find(AutomationIds.GoToQuery);

    /// <summary>A result row by its title.</summary>
    public UiElement Row(string title) => Find(AutomationIds.GoToRow, name: title);

    /// <summary>The titles of the listed results.</summary>
    public async Task<IReadOnlyList<string>> RowTitlesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.GoToRow) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    /// <summary>Waits until a result whose title contains <paramref name="part"/> is listed and returns its title.</summary>
    public async Task<string> WaitForRowContainingAsync(string part, CancellationToken cancellationToken) =>
        (await UiWait.ForAsync(Driver, () => RowTitlesAsync(cancellationToken), rows => rows.Any(r => r.Contains(part, StringComparison.Ordinal)),
            $"a result containing '{part}'", cancellationToken)).First(r => r.Contains(part, StringComparison.Ordinal));
}
