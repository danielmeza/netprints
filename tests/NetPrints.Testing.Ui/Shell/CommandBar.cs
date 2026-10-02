using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the shell's command bar: buttons by command id.</summary>
public sealed class CommandBar(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.ShellCommandBar) { Within = window })
{
    /// <summary>The button of a command.</summary>
    public UiElement Button(string commandId) => Find(AutomationIds.CommandBarPrefix + commandId);

    /// <summary>Clicks the button of a command.</summary>
    public Task InvokeAsync(string commandId, CancellationToken cancellationToken) => Button(commandId).ClickAsync(cancellationToken);
}
