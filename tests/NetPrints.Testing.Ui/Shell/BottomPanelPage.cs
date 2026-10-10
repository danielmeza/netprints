using NetPrints.Editor;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the bottom tool dock: the Errors, Output and C# panels, found by panel id.</summary>
public sealed class BottomPanelPage(IUiDriver driver, AutomationQuery window) : UiElement(driver, window)
{
    private static readonly TimeSpan BuildBudget = TimeSpan.FromSeconds(120);

    /// <summary>The tab of a panel; Dock names it by the panel id.</summary>
    public UiElement Tab(string panelId) => new(Driver, new AutomationQuery(panelId) { Within = Query });

    /// <summary>The content of a panel while its tab is selected.</summary>
    public UiElement Panel(string panelId) => Find(AutomationIds.ShellPanelPrefix + panelId);

    public UiElement ErrorsList => Panel(PanelContributions.ErrorsId).Find(AutomationIds.ErrorsList);

    /// <summary>A row of the Errors panel (one per diagnostic, errors first); <paramref name="index"/> counts from the top.</summary>
    public UiElement ErrorRow(int index = 0) => ErrorsList.Find(AutomationIds.ErrorsRow, index: index);

    public UiElement ErrorsEmpty => Panel(PanelContributions.ErrorsId).Find(AutomationIds.ErrorsEmpty);

    public UiElement OutputList => Panel(PanelContributions.OutputId).Find(AutomationIds.OutputLines);

    public UiElement CSharpCode => Panel(PanelContributions.CSharpId).Find(AutomationIds.CSharpCode);

    /// <summary>Selects a panel's tab (when its content is not shown) and waits for the content.</summary>
    public async Task ShowAsync(string panelId, CancellationToken cancellationToken)
    {
        var panel = Panel(panelId);
        if (!await panel.IsVisibleAsync(cancellationToken))
        {
            await Tab(panelId).ClickAsync(cancellationToken);
            await panel.WaitVisibleAsync(cancellationToken);
        }
    }

    /// <summary>The lines of the Output panel, oldest first.</summary>
    public async Task<IReadOnlyList<string>> OutputLinesAsync(CancellationToken cancellationToken)
    {
        var lines = await Driver.FindAllAsync(new AutomationQuery(AutomationIds.OutputLine) { Within = OutputList.Query }, cancellationToken);
        return lines.Select(line => line.Text ?? "").ToList();
    }

    /// <summary>The last build result line of the Output panel, or null when there is none.</summary>
    public async Task<string?> BuildResultAsync(CancellationToken cancellationToken) =>
        (await OutputLinesAsync(cancellationToken)).LastOrDefault(IsBuildResult);

    /// <summary>
    /// Waits until the Output panel holds a build result line ("Build succeeded", "Build failed with …") and returns it. With
    /// <paramref name="before"/> (the lines read before the build was triggered) the lines must also differ from it, so a result left by
    /// an earlier build is not taken for the new one.
    /// </summary>
    public async Task<string> WaitForBuildResultAsync(CancellationToken cancellationToken, IReadOnlyList<string>? before = null)
    {
        await ShowAsync(PanelContributions.OutputId, cancellationToken);
        var lines = await UiWait.ForAsync(Driver, () => OutputLinesAsync(cancellationToken),
            current => current.Any(IsBuildResult) && (before is null || !current.SequenceEqual(before)), "a build result in Output", cancellationToken, BuildBudget);
        return lines.Last(IsBuildResult);
    }

    /// <summary>Waits until a line of the Output panel contains <paramref name="expected"/> and returns the lines.</summary>
    public async Task<IReadOnlyList<string>> WaitForOutputContainingAsync(string expected, CancellationToken cancellationToken)
    {
        return await UiWait.ForAsync(Driver, () => OutputLinesAsync(cancellationToken), Any(line => line.Contains(expected, StringComparison.Ordinal)),
            $"output containing '{expected}'", cancellationToken, BuildBudget);
    }

    private static bool IsBuildResult(string line) =>
        line.StartsWith("Build succeeded", StringComparison.Ordinal) || line.StartsWith("Build failed", StringComparison.Ordinal);

    private static Func<IReadOnlyList<string>, bool> Any(Func<string, bool> predicate) => lines => lines.Any(predicate);
}
