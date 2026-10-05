using NetPrints.Editor;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the Inspector panel: its empty state and the inspector of the selected item.</summary>
public sealed class InspectorPage(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.ShellPanelPrefix + PanelContributions.InspectorId) { Within = window, IncludeHidden = true })
{
    /// <summary>The text shown while nothing is selected.</summary>
    public UiElement Empty => Find(AutomationIds.InspectorEmpty);

    /// <summary>The host of the inspector of the selected item.</summary>
    public UiElement Content => Find(AutomationIds.InspectorContent);

    /// <summary>The class inspector.</summary>
    public UiElement ClassInspector => Find(AutomationIds.ClassInspector);

    /// <summary>The class inspector's name box.</summary>
    public UiElement ClassName => Find(AutomationIds.ClassInspectorName);

    /// <summary>The class inspector's generated code preview.</summary>
    public UiElement ClassCodeView => Find(AutomationIds.ClassInspectorCodeView);

    /// <summary>Waits until the class inspector's code view shows the generated code and its syntax highlighting has caught up with it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="timeout">Longest to wait; defaults to the harness' wait budget.</param>
    public Task WaitClassCodeHighlightedAsync(CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        ClassCodeView.WaitUntilAsync(
            e => !string.IsNullOrEmpty(e.Text) && e[AutomationPropertyNames.HighlightingSettled] == bool.TrueString,
            "generated code highlighted", cancellationToken, timeout);

    /// <summary>The method inspector.</summary>
    public UiElement MethodInspector => Find(AutomationIds.MethodInspector);

    /// <summary>The variable inspector.</summary>
    public UiElement VariableInspector => Find(AutomationIds.VariableInspector);
}
