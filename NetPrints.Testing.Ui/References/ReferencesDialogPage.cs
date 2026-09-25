using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.References;

/// <summary>Screen object of the references dialog (PAR-15..21).</summary>
public sealed class ReferencesDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.ReferencesDialog))
{
    public UiElement AddAssemblyButton => Find(AutomationIds.ReferencesAddAssemblyButton);
    public UiElement AddSourceButton => Find(AutomationIds.ReferencesAddSourceButton);
    public UiElement CloseButton => Find(AutomationIds.ReferencesCloseButton);

    /// <summary>A reference row by its display text.</summary>
    public UiElement Row(string displayText) => Find(AutomationIds.ReferenceRow, name: displayText);

    public UiElement IncludeSwitch(string displayText) => Row(displayText).Find(AutomationIds.ReferenceInclude);

    public UiElement RemoveButton(string displayText) => Row(displayText).Find(AutomationIds.ReferenceRemove);

    public async Task<IReadOnlyList<string>> RowNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.ReferenceRow) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    /// <summary>Waits until a row whose display text contains <paramref name="fragment"/> is listed.</summary>
    public async Task<string> WaitForRowAsync(string fragment, CancellationToken cancellationToken) =>
        (await UiWait.ForAsync(Driver, () => RowNamesAsync(cancellationToken), rows => rows.Any(r => r.Contains(fragment, StringComparison.Ordinal)),
            $"a reference row containing '{fragment}'", cancellationToken)).First(r => r.Contains(fragment, StringComparison.Ordinal));

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await CloseButton.ClickAsync(cancellationToken);
        await UiWait.UntilAsync(Driver, async () => !await ExistsAsync(cancellationToken), "references dialog closed", cancellationToken);
    }
}
