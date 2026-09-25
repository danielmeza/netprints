using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Graph;

/// <summary>Component object of the Get/Set chooser popup shown when a variable is dropped (PAR-55).</summary>
public sealed class GetSetChooser(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.GraphGetSetPopup) { Within = window, IncludeHidden = true })
{
    public UiElement View => new(Driver, new AutomationQuery(AutomationIds.GetSetChooser) { Within = Query });
    public UiElement GetButton => new(Driver, new AutomationQuery(AutomationIds.GetButton) { Within = Query });
    public UiElement SetButton => new(Driver, new AutomationQuery(AutomationIds.SetButton) { Within = Query });

    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken) => await PropertyAsync(AutomationPropertyNames.IsOpen, cancellationToken) == "True";

    public Task WaitOpenAsync(CancellationToken cancellationToken) => WaitUntilAsync(e => e[AutomationPropertyNames.IsOpen] == "True", "open", cancellationToken);

    public Task WaitClosedAsync(CancellationToken cancellationToken) => WaitUntilAsync(e => e[AutomationPropertyNames.IsOpen] == "False", "closed", cancellationToken);
}
