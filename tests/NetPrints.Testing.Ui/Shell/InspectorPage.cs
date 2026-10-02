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

    /// <summary>The method inspector.</summary>
    public UiElement MethodInspector => Find(AutomationIds.MethodInspector);

    /// <summary>The variable inspector.</summary>
    public UiElement VariableInspector => Find(AutomationIds.VariableInspector);
}
