using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Graph;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>
/// Screen object of the shell window: its bars and panels by id, documents by document id and
/// commands by command id. Dock's own elements are never queried by name.
/// </summary>
public sealed class ShellPage(IUiDriver driver)
    : UiElement(driver, new AutomationQuery(AutomationIds.ShellWindow))
{
    public MenuBar Menu => new(Driver, Query);

    public CommandBar Commands => new(Driver, Query);

    public ProjectTreePage Tree => new(Driver, Query);

    public DocumentTabsPage Tabs => new(Driver, Query);

    public InspectorPage Inspector => new(Driver, Query);

    public BottomPanelPage Bottom => new(Driver, Query);

    /// <summary>The graph canvas of the selected graph document.</summary>
    public GraphCanvas Graph => new(Driver, Query);

    public UiElement StatusMessage => Find(AutomationIds.ShellStatusMessage);

    public UiElement BuildState => Find(AutomationIds.ShellBuildState);

    /// <summary>The window title.</summary>
    public async Task<string?> TitleAsync(CancellationToken cancellationToken) => await TextAsync(cancellationToken);

    /// <summary>Waits until the shell window is shown.</summary>
    public async Task<ShellPage> WaitShownAsync(CancellationToken cancellationToken)
    {
        await GetAsync(cancellationToken);
        return this;
    }

    /// <summary>Waits until the project <paramref name="name"/> is open (in the tree) and its window title says so.</summary>
    public async Task WaitForProjectAsync(string name, CancellationToken cancellationToken)
    {
        await Tree.WaitForProjectAsync(name, cancellationToken);
        await WaitUntilAsync(e => e.Text?.Contains(name, StringComparison.Ordinal) == true, $"title with '{name}'", cancellationToken);
    }
}
