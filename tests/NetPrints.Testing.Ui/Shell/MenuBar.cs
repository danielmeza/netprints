using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the shell's menu bar: menus by name, items by command id.</summary>
public sealed class MenuBar(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.ShellMenuBar) { Within = window })
{
    /// <summary>A top-level menu by its name, such as "File".</summary>
    public UiElement Menu(string name) => Find(AutomationIds.MenuPrefix + name);

    /// <summary>A menu item by its command id; its popup is not inside the window.</summary>
    public UiElement Item(string commandId) => new(Driver, new AutomationQuery(AutomationIds.MenuPrefix + commandId));

    /// <summary>Opens <paramref name="menu"/> and waits until the item of <paramref name="commandId"/> is shown.</summary>
    public async Task<UiElement> OpenAsync(string menu, string commandId, CancellationToken cancellationToken)
    {
        await Menu(menu).ClickAsync(cancellationToken);
        var item = Item(commandId);
        await item.WaitVisibleAsync(cancellationToken);
        return item;
    }

    /// <summary>Opens <paramref name="menu"/> and clicks the item of <paramref name="commandId"/>.</summary>
    public async Task InvokeAsync(string menu, string commandId, CancellationToken cancellationToken) =>
        await (await OpenAsync(menu, commandId, cancellationToken)).ClickAsync(cancellationToken);
}
