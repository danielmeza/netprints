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

    /// <summary>The event graph inspector.</summary>
    public UiElement EventGraphInspector => Find(AutomationIds.EventGraphInspector);

    /// <summary>The event graph inspector's name box.</summary>
    public UiElement EventGraphName => Find(AutomationIds.EventGraphInspectorName);

    /// <summary>The message of a refused event graph name.</summary>
    public UiElement EventGraphError => Find(AutomationIds.EventGraphInspectorError);

    /// <summary>The event entry inspector.</summary>
    public UiElement EventEntryInspector => Find(AutomationIds.EventEntryInspector);

    /// <summary>The event entry inspector's name box.</summary>
    public UiElement EventEntryName => Find(AutomationIds.EventEntryInspectorName);

    /// <summary>The kind line of the event entry inspector.</summary>
    public UiElement EventEntryKind => Find(AutomationIds.EventEntryInspectorKind);

    /// <summary>The message of a refused event entry edit.</summary>
    public UiElement EventEntryError => Find(AutomationIds.EventEntryInspectorError);

    /// <summary>The note on where an override's signature comes from.</summary>
    public UiElement EventEntryBaseSignature => Find(AutomationIds.EventEntryInspectorBaseSignature);

    /// <summary>The add argument button of the event entry inspector.</summary>
    public UiElement EventEntryAddArgument => Find(AutomationIds.EventEntryInspectorAddArgument);

    /// <summary>The name boxes of the argument rows.</summary>
    public UiElement EventEntryArgumentName => Find(AutomationIds.EventEntryInspectorArgumentName);

    /// <summary>How many argument rows the event entry inspector lists.</summary>
    public async Task<int> EventEntryArgumentCountAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.EventEntryInspectorArgumentName) { Within = Query }, cancellationToken)).Count;
}
