using NetPrints.Editor;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the Variables panel's "Method: &lt;name&gt;" group: the local variables of the active graph.</summary>
public sealed class VariablesPage(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.ShellPanelPrefix + PanelContributions.VariablesId) { Within = window, IncludeHidden = true })
{
    private UiElement MethodGroup => Find(AutomationIds.VariablesMethodGroup);

    /// <summary>The button that adds a local variable to the active graph.</summary>
    public UiElement AddLocalButton => Find(AutomationIds.VariablesAddLocalVariable);

    /// <summary>A local variable row; its automation name is the local's name.</summary>
    public UiElement LocalVariable(string name) => MethodGroup.Find(AutomationIds.VariableRow, name: name);

    /// <summary>The name box of a local variable row.</summary>
    public UiElement LocalVariableNameBox(string name) => LocalVariable(name).Find(AutomationIds.VariableName);

    /// <summary>The names of the local variable rows shown.</summary>
    public async Task<IReadOnlyList<string>> LocalVariableNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.VariableRow) { Within = MethodGroup.Query }, cancellationToken))
            .Select(e => e.Name ?? "").ToList();

    /// <summary>Clicks Add local variable and returns the new row's default name (Local, Local1, ...).</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The new row's name.</returns>
    public async Task<string> AddLocalAsync(CancellationToken cancellationToken)
    {
        var before = await LocalVariableNamesAsync(cancellationToken);
        await AddLocalButton.ClickAsync(cancellationToken);
        var after = await UiWait.ForAsync(Driver, () => LocalVariableNamesAsync(cancellationToken),
            names => names.Count == before.Count + 1, "a new local variable row", cancellationToken);
        return after.Except(before).Single();
    }
}
