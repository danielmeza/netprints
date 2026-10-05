using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
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

    public VariablesPage Variables => new(Driver, Query);

    public BottomPanelPage Bottom => new(Driver, Query);

    /// <summary>The graph canvas of the selected graph document.</summary>
    public GraphCanvas Graph => new(Driver, Query);

    /// <summary>The canvas of a graph document wherever it is shown: docked in this window or floated in its own.</summary>
    public GraphCanvas GraphOf(DocumentId document) => new(Driver, new AutomationQuery(AutomationIds.ShellDocumentPrefix + document));

    /// <summary>The tab of a panel in this window (Dock names it by the panel id); it is not here while the panel floats.</summary>
    public UiElement PanelTab(string panelId) => Find(panelId);

    /// <summary>The content of a panel wherever it is shown: docked in this window or floated in its own.</summary>
    public UiElement PanelContent(string panelId) => new(Driver, new AutomationQuery(AutomationIds.ShellPanelPrefix + panelId));

    public UiElement StatusMessage => Find(AutomationIds.ShellStatusMessage);

    public UiElement BuildState => Find(AutomationIds.ShellBuildState);

    /// <summary>The window title.</summary>
    public async Task<string?> TitleAsync(CancellationToken cancellationToken) => await TextAsync(cancellationToken);

    /// <summary>Opens a method's graph from the Project tree and waits until its canvas is shown.</summary>
    public async Task OpenMethodAsync(string method, CancellationToken cancellationToken)
    {
        var row = await Tree.RevealAsync(Tree.Method(method), ProjectTreePage.MethodsGroup, cancellationToken);
        await Tree.OpenAsync(row, async () => (await Graph.Watermark.TryGetAsync(cancellationToken))?.Text == method, $"graph '{method}' shown", cancellationToken);
        await Graph.WaitForGraphAsync(method, cancellationToken);
    }

    /// <summary>The type names of the editor's open windows (dialogs included), in the order they opened.</summary>
    public async Task<IReadOnlyList<string>> WindowTypesAsync(CancellationToken cancellationToken) =>
        (await Driver.DumpAsync(cancellationToken)).Split('\n').Where(line => line.StartsWith("[w", StringComparison.Ordinal))
        .Select(line => line.Split(' ', 3)[1]).ToList();

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
