using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the Project tree panel: rows by kind and name (<c>Tree.&lt;kind&gt;.&lt;name&gt;</c>).</summary>
public sealed class ProjectTreePage(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.TreeView) { Within = window })
{
    private const double HeaderOffset = 60;
    private const double HeaderHeight = 32;
    private static readonly TimeSpan OpenBudget = TimeSpan.FromSeconds(10);

    public const string MethodsGroup = "Methods";
    public const string ConstructorsGroup = "Constructors";
    public const string VariablesGroup = "Variables";
    public const string EventGraphsGroup = "Event graphs";

    /// <summary>A row by its kind (<see cref="AutomationIds.TreeKindMethod"/> and the like) and name.</summary>
    public UiElement Item(string kind, string name) => Find(AutomationIds.TreePrefix + kind + "." + name);

    public UiElement Project(string name) => Item(AutomationIds.TreeKindProject, name);

    public UiElement Class(string name) => Item(AutomationIds.TreeKindClass, name);

    public UiElement Method(string name) => Item(AutomationIds.TreeKindMethod, name);

    public UiElement Constructor(string name) => Item(AutomationIds.TreeKindConstructor, name);

    public UiElement Variable(string name) => Item(AutomationIds.TreeKindVariable, name);

    public UiElement Group(string name) => Item(AutomationIds.TreeKindGroup, name);

    /// <summary>Waits until the project <paramref name="name"/> is in the tree.</summary>
    public Task WaitForProjectAsync(string name, CancellationToken cancellationToken) =>
        Project(name).WaitVisibleAsync(cancellationToken, TimeSpan.FromSeconds(60));

    /// <summary>Expands <paramref name="group"/> when <paramref name="row"/> is not shown yet (a collapsed group realizes no rows).</summary>
    public async Task<UiElement> RevealAsync(UiElement row, string group, CancellationToken cancellationToken)
    {
        if (!await row.IsVisibleAsync(cancellationToken))
        {
            await SelectAsync(Group(group), cancellationToken);
            await Driver.PressAsync("Right", cancellationToken);
            await row.WaitVisibleAsync(cancellationToken);
        }

        return row;
    }

    /// <summary>Double-clicks a method row, expanding the Methods group first, and opens its graph.</summary>
    public async Task OpenMethodAsync(string name, CancellationToken cancellationToken) =>
        await (await RevealAsync(Method(name), MethodsGroup, cancellationToken)).DoubleClickAsync(cancellationToken);

    /// <summary>Double-clicks a row once and waits for <paramref name="isOpen"/>; a double click that does not open fails the wait, with the driver's input trace.</summary>
    public async Task OpenAsync(UiElement row, Func<Task<bool>> isOpen, string what, CancellationToken cancellationToken)
    {
        await row.DoubleClickAsync(cancellationToken);
        await UiWait.UntilAsync(Driver, isOpen, what, cancellationToken, OpenBudget);
    }

    /// <summary>Clicks a row's header; the bounds of an expanded row include its children, so its center is not its header.</summary>
    public async Task SelectAsync(UiElement row, CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await row.OffsetAsync(HeaderOffset, HeaderHeight / 2, cancellationToken), UiButton.Left, 1, cancellationToken);
}
