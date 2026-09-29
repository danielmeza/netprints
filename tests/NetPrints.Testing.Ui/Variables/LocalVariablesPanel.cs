using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Variables;

/// <summary>Component object of the Variables panel's "Method: &lt;name&gt;" group (US5, ED-T06).</summary>
public sealed class LocalVariablesPanel(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.VariablesMethodGroup) { Within = window, IncludeHidden = true })
{
    public UiElement CreateButton => new(Driver, new AutomationQuery(AutomationIds.CreateLocalVariableButton) { Within = window });

    /// <summary>A local variable row. Its AutomationProperties.Name carries the local's name.</summary>
    public UiElement LocalVariable(string name) => Find(AutomationIds.VariableRow, name: name);

    /// <summary>The name box of a local variable row.</summary>
    public UiElement LocalVariableNameBox(string name) => LocalVariable(name).Find(AutomationIds.VariableName);

    public async Task<IReadOnlyList<string>> LocalVariableNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.VariableRow) { Within = Query }, cancellationToken))
            .Select(e => e.Name ?? "").ToList();

    /// <summary>Clicks Create and returns the new row's default name (Local, Local1, ...).</summary>
    public async Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        var before = await LocalVariableNamesAsync(cancellationToken);
        await CreateButton.ClickAsync(cancellationToken);
        var after = await UiWait.ForAsync(Driver, () => LocalVariableNamesAsync(cancellationToken),
            names => names.Count == before.Count + 1, "a new local variable row", cancellationToken);
        return after.Except(before).Single();
    }
}
